using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace Config.CustomProvider.Web
{
    public class MyConfigProvider : IConfigurationProvider
    {
        public bool TryGet(string key, out string value)
        {
            switch (key)
            {
                case "Hardcoded:1:Caption":
                    value = "One";
                    return true;
                case "Hardcoded:2:Caption":
                    value = "Two";
                    return true;
            }
            value = null;
            return false;
        }

        public void Load()
        {
            // no loading or reloading, this source is all hardcoded
        }
        
        public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string prefix)
        {
            // TODO: This method signature is pretty bad

            if (string.IsNullOrEmpty(prefix))
            {
                return earlierKeys.Concat(new[] { "Hardcoded" });
            }
            if (prefix == "Hardcoded")
            {
                return earlierKeys.Concat(new[] { "1", "2" });
            }
            if (prefix == "Hardcoded:1" || prefix == "Hardcoded:2")
            {
                return earlierKeys.Concat(new[] { "Caption" });
            }
            return earlierKeys;
        }

        public void Set(string key, string value)
        {
        }
    }
}