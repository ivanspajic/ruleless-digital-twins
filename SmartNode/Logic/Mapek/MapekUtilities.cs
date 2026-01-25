using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;

namespace Logic.Mapek {
    internal static class MapekUtilities {
        public static string GetSimpleName(string longName) {
            string simpleName;
            var simpleNameArray = longName.Split('#');

            // Check if the name URI ends with a '/' instead of a '#'.
            if (simpleNameArray.Length == 1) {
                simpleName = longName.Split('/')[^1];
            } else {
                simpleName = simpleNameArray[1];
            }

            return simpleName;
        }

        public static Property GetPropertyFromPropertyCacheByName(PropertyCache propertyCache, string propertyName) {
            if (propertyCache.Properties.TryGetValue(propertyName, out Property? property)) {
                return property;
            } else if (propertyCache.ConfigurableParameters.TryGetValue(propertyName, out ConfigurableParameter? configurableParameter)) {
                return configurableParameter;
            }
            
            throw new Exception($"Property {propertyName} not found in the property cache.");
        }        
    }
}
