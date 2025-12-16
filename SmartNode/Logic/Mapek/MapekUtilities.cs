using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;

namespace Logic.Mapek
{
    internal static class MapekUtilities
    {
        public static string GetSimpleName(string longName)
        {
            var simpleName = string.Empty;
            var simpleNameArray = longName.Split('#');

            // Check if the name URI ends with a '/' instead of a '#'.
            if (simpleNameArray.Length == 1)
            {
                simpleName = longName.Split('/')[^1];
            }
            else
            {
                simpleName = simpleNameArray[1];
            }

            return simpleName;
        }

        public static Property GetPropertyFromPropertyCacheByName(Cache propertyCache, string propertyName)
        {
            if (!propertyCache.Properties.TryGetValue(propertyName, out Property? property))
            {
                property = propertyCache.ConfigurableParameters[propertyName];
            }

            return property;
        }        
    }
}
