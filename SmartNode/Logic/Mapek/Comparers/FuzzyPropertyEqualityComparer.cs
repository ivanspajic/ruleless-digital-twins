using Logic.Models.OntologicalModels;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Logic.Mapek.Comparers {
    internal class FuzzyPropertyEqualityComparer : IEqualityComparer<Property> {
        private readonly double _fuzzinessFactor;

        public FuzzyPropertyEqualityComparer(double fuzzinessFactor) {
            _fuzzinessFactor = fuzzinessFactor;
        }

        public bool Equals(Property? x, Property? y) {
            bool valuesWithinRange;
            // No fuzzy non-numericals!

            //// This is a workaround for CBR testing due to no matches being possible on accumulated properties.
            //if (x!.Name.Equals("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumption") && y!.Name.Equals("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumption")) {
            //    return true;
            //}
            //if (x!.Name.Equals("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeterMeasuredEnergyConsumption") && y!.Name.Equals("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeterMeasuredEnergyConsumption")) {
            //    return true;
            //}

            if ((!x!.OwlType.Equals("http://www.w3.org/2001/XMLSchema#double") || !y!.OwlType.Equals("http://www.w3.org/2001/XMLSchema#double")) &&
                (!x!.OwlType.Equals("http://www.w3.org/2001/XMLSchema#int") || !y!.OwlType.Equals("http://www.w3.org/2001/XMLSchema#int"))) {
                valuesWithinRange = true;
            } else {
                if (x.Value is not double) {
                    x.Value = double.Parse(x.Value.ToString()!, CultureInfo.InvariantCulture);
                }
                if (y.Value is not double) {
                    y.Value = double.Parse(y.Value.ToString()!, CultureInfo.InvariantCulture);
                }
                valuesWithinRange = GetIfValuesWithinRange((double)x!.Value, (double)y!.Value, _fuzzinessFactor);
            }

            return x.Name.Equals(y.Name) && valuesWithinRange && x.OwlType.Equals(y.OwlType);
        }

        public int GetHashCode([DisallowNull] Property obj) {
            return obj.Name.GetHashCode() *
                //obj.Value.GetHashCode() *
                obj.OwlType.GetHashCode();
        }

        private static bool GetIfValuesWithinRange(double propertyXValue, double propertyYValue, double fuzzinessFactor) {
            return Math.Abs(propertyXValue - propertyYValue) <= fuzzinessFactor;
        }
    }
}
