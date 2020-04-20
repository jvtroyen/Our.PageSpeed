using System.Collections.Generic;
using System.Text;
using System.Web.Routing;

namespace Our.PageSpeed.ActionFilters
{
    public class KeyBuilder : IKeyBuilder
    {
        private string _cacheKeyPrefix = "_l4zyl04der.";

        public string CacheKeyPrefix
        {
            get
            {
                return this._cacheKeyPrefix;
            }
            set
            {
                this._cacheKeyPrefix = value;
            }
        }

        public string BuildKey(string controllerName)
        {
            return this.BuildKey(controllerName, (string)null, (RouteValueDictionary)null);
        }

        public string BuildKey(string controllerName, string actionName)
        {
            return this.BuildKey(controllerName, actionName, (RouteValueDictionary)null);
        }

        public string BuildKey(string controllerName, string actionName, RouteValueDictionary routeValues)
        {
            StringBuilder stringBuilder = new StringBuilder(this.CacheKeyPrefix);
            if (controllerName != null)
                stringBuilder.AppendFormat("{0}.", (object)controllerName.ToLowerInvariant());
            if (actionName != null)
                stringBuilder.AppendFormat("{0}#", (object)actionName.ToLowerInvariant());
            if (routeValues != null)
            {
                foreach (KeyValuePair<string, object> routeValue in routeValues)
                    stringBuilder.Append(this.BuildKeyFragment(routeValue));
            }
            return stringBuilder.ToString();
        }

        public string BuildKeyFragment(KeyValuePair<string, object> routeValue)
        {
            string str = routeValue.Value == null ? "<null>" : routeValue.Value.ToString().ToLowerInvariant();
            return string.Format("{0}={1}#", (object)routeValue.Key.ToLowerInvariant(), (object)str);
        }
    }
}
