using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Mvc;
using HtmlAgilityPack;
using Umbraco.Core.Composing;

namespace Our.Umbraco.PageSpeed.ActionFilters
{
    public class LazyLoadFilterAttribute : ActionFilterAttribute
    {
        //TODO: put these in app.config, with default fallbacks to webp and quality 70
        private const string FilterPrefix = "/media/";
        private const string WebpConversion = "format=webp&quality=70";
        private static string[] crawlerBots = new string[] { "Googlebot", "Screaming Frog" };
        private readonly IKeyGenerator keyGenerator;

        public LazyLoadFilterAttribute()
            : this(new KeyBuilder())
        {
        }

        public LazyLoadFilterAttribute(IKeyBuilder keyBuilder)
            : this(new KeyGenerator(keyBuilder))
        {
            var crawlerBotsSetting = ConfigurationManager.AppSettings["PageSpeed.CrawlerBots"];
            if (!string.IsNullOrEmpty(crawlerBotsSetting))
            {
                crawlerBots = crawlerBotsSetting.Split(';');
            }
        }

        public LazyLoadFilterAttribute(IKeyGenerator keyGenerator)
        {
            this.keyGenerator = keyGenerator;

            this.Order = (int)FilterScope.Last;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            //We aren't interested in child-actions, as we are processing ALL images when the complete result is returned 
            if (filterContext.IsChildAction)
            {
                return;
            }

            if (crawlerBots.Any(x => filterContext?.RequestContext?.HttpContext?.Request?.UserAgent?.Contains(x) ?? false))
            {
                return;
            }

            string cacheKey = keyGenerator.GenerateKey(filterContext);

            //Keep track of original writer
            var originalWriter = filterContext.HttpContext.Response.Output;

            //Create new writer and hijack the response
            var lazyWriter = new StringWriter(CultureInfo.InvariantCulture);
            filterContext.HttpContext.Response.Output = lazyWriter;

            //Create callback method that wil be executed OnResult
            filterContext.HttpContext.Items[cacheKey] = (Action<bool>)(hasErrors =>
            {
                //Remove callback
                filterContext.HttpContext.Items.Remove(cacheKey);

                //Restore original writer
                filterContext.HttpContext.Response.Output = originalWriter;
                if (hasErrors)
                {
                    return;
                }

                //Process response
                var response = lazyWriter.ToString();
                if (!string.IsNullOrEmpty(response))
                {
                    var result = ProcessResponse(response, cacheKey);

                    //Write response
                    filterContext.HttpContext.Response.Write(result);
                }
            });
        }

        public override void OnResultExecuted(ResultExecutedContext filterContext)
        {
            if (filterContext.IsChildAction)
            {
                return;
            }

            string cacheKey = keyGenerator.GenerateKey(filterContext);
            if (string.IsNullOrEmpty(cacheKey))
            {
                return;
            }

            var action = filterContext.HttpContext.Items[cacheKey] as Action<bool>;
            if (action == null)
            {
                return;
            }

            //Execute callback
            var hasErrors = filterContext.Exception != null;
            action(hasErrors);
        }

        private static string ProcessResponse(string response, string cacheKey)
        {
            using (Current.ProfilingLogger.DebugDuration<LazyLoadFilterAttribute>("Replacing images - " + cacheKey))
            {
                //Find all images and replace them with picture
                var document = new HtmlDocument();
                document.LoadHtml(response);

                var nodes = document.DocumentNode.SelectNodes("//img | //source");
                if (nodes != null)
                {
                    nodes
                        .Where(x => !x.GetClasses().Contains("lazyload"))
                        .ToList()
                        .ForEach(x =>
                        {
                            if (x.Name.Equals("img", StringComparison.OrdinalIgnoreCase))
                            {
                                if (x.GetAttributeValue("src", null)?.StartsWith(FilterPrefix, StringComparison.OrdinalIgnoreCase) ?? false)
                                {
                                    ReplaceImgWithPicture(x);
                                }
                                else
                                {
                                    ReplaceSrc(x);
                                }
                            }
                            else
                            {
                                ReplaceSrc(x);
                            }
                        });
                }

                return document.DocumentNode.OuterHtml;
            }
        }

        /*
         * Replace img tag with picture tag
         * 
         * <img src="image.png" />
         * 
         * becomes
         * 
         * <picture>
         *   <source type="image/webp" src="image.png?format=webp" />
         *   <source src="image.png" />
         * </picture>
         */
        private static void ReplaceImgWithPicture(HtmlNode img)
        {
            if (!img.ParentNode.Name.Equals("picture", System.StringComparison.OrdinalIgnoreCase))
            {
                var picture = CreatePicture(img);

                var parent = img.ParentNode;
                parent.ReplaceChild(picture, img);
            }
        }

        private static HtmlNode CreatePicture(HtmlNode img)
        {
            var picture = HtmlNode.CreateNode("<picture>");
            CopyAttributes(img, picture, new string[] { "src", "srcset", "sizes", "ratio", "data-src", "data-srcset", "data-sizes", "data-ratio" });

            var src = img.GetAttributeValue("src", null) ?? img.GetAttributeValue("data-src", null);
            var srcset = img.GetAttributeValue("srcset", null) ?? img.GetAttributeValue("data-srcset", null);
            var sizes = img.GetAttributeValue("sizes", null) ?? img.GetAttributeValue("data-sizes", null);

            picture.AppendChild(CreatePictureSource(src, srcset, sizes, true));
            picture.AppendChild(CreatePictureSource(src, srcset, sizes));

            return picture;
        }

        private static void CopyAttributes(HtmlNode source, HtmlNode target, string[] exceptions)
        {
            foreach (var attrib in source.Attributes)
            {
                if (!exceptions.Contains(attrib.Name, StringComparer.OrdinalIgnoreCase))
                {
                    target.SetAttributeValue(attrib.Name, attrib.Value);
                }
            }
        }

        private static HtmlNode CreatePictureSource(string src, string srcset, string sizes, bool webp = false)
        {
            var source = webp ? HtmlNode.CreateNode("<source>") : HtmlNode.CreateNode("<img>");
            if (webp)
            {
                source.SetAttributeValue("type", "image/webp");
                src += src.Contains("?") ? "&" + WebpConversion : "?" + WebpConversion;
            }

            source.AddClass("lazyload");
            if (!string.IsNullOrEmpty(src))
            {
                //Yes, srcSET, not src
                source.SetAttributeValue("data-srcset", src);
            }

            if (!string.IsNullOrEmpty(srcset))
            {
                source.SetAttributeValue("data-srcset", srcset);
            }

            if (!string.IsNullOrEmpty(sizes))
            {
                source.SetAttributeValue("data-sizes", sizes);
            }

            return source;
        }

        private static void ReplaceSrc(HtmlNode node)
        {
            var src = node.GetAttributeValue("src", string.Empty);
            var srcset = node.GetAttributeValue("srcset", string.Empty);
            var sizes = node.GetAttributeValue("sizes", string.Empty);

            node.AddClass("lazyload");
            if (!string.IsNullOrEmpty(src))
            {
                node.SetAttributeValue("data-src", src);
                node.Attributes.Remove("src");
            }

            if (!string.IsNullOrEmpty(srcset))
            {
                node.SetAttributeValue("data-srcset", srcset);
                node.Attributes.Remove("srcset");
            }

            if (!string.IsNullOrEmpty(sizes))
            {
                node.SetAttributeValue("data-sizes", sizes);
                node.Attributes.Remove("sizes");
            }
        }
    }
}