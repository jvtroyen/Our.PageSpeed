using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;

namespace Our.PageSpeed.ActionFilters
{
    public class KeyGenerator : IKeyGenerator
    {
        internal const string RouteDataKeyAction = "action";
        internal const string RouteDataKeyController = "controller";
        internal const string DataTokensKeyArea = "area";
        private readonly IKeyBuilder _keyBuilder;

        public KeyGenerator(IKeyBuilder keyBuilder)
        {
            if (keyBuilder == null)
                throw new ArgumentNullException(nameof(keyBuilder));
            this._keyBuilder = keyBuilder;
        }

        public string GenerateKey(ControllerContext context)
        {
            RouteData routeData = context.RouteData;
            if (routeData == null)
                return (string)null;
            string actionName = (string)null;
            string controllerName = (string)null;
            if (routeData.Values.ContainsKey("action") && routeData.Values["action"] != null)
                actionName = routeData.Values["action"].ToString();
            if (routeData.Values.ContainsKey("controller") && routeData.Values["controller"] != null)
                controllerName = routeData.Values["controller"].ToString();
            if (string.IsNullOrEmpty(actionName) || string.IsNullOrEmpty(controllerName))
                return (string)null;
            string str1 = (string)null;
            if (routeData.DataTokens.ContainsKey("area") && routeData.DataTokens["area"] != null)
                str1 = routeData.DataTokens["area"].ToString();
            List<KeyValuePair<string, object>> list = routeData.Values.Where<KeyValuePair<string, object>>((Func<KeyValuePair<string, object>, bool>)(x =>
            {
                if (x.Key.ToLowerInvariant() != "controller" && x.Key.ToLowerInvariant() != "action" && x.Key.ToLowerInvariant() != "area")
                    return !(x.Value is DictionaryValueProvider<object>);
                return false;
            })).ToList<KeyValuePair<string, object>>();
            if (!string.IsNullOrWhiteSpace(str1))
                list.Add(new KeyValuePair<string, object>("area", (object)str1));
            List<KeyValuePair<string, object>> source1 = list;
            Func<KeyValuePair<string, object>, string> keySelector1 = (Func<KeyValuePair<string, object>, string>)(x => x.Key.ToLowerInvariant());
            RouteValueDictionary routeValueDictionary = new RouteValueDictionary((IDictionary<string, object>)source1.ToDictionary<KeyValuePair<string, object>, string, object>(keySelector1, (Func<KeyValuePair<string, object>, object>)(x => x.Value)));

            return this._keyBuilder.BuildKey(controllerName, actionName, routeValueDictionary);
        }
    }
}