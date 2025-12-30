using Femyou;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;

namespace TestProject.Mocks
{
    internal class IntValueHandlerMock : IValueHandler
    {
        private static readonly Dictionary<string, IEnumerable<object>> _actuatorStatePossibleValues = new()
        {
            { "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AirConditioningUnit", new List<object>
                {
                    1,
                    2,
                    3
                }
            },
            { "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier", new List<object>
                {
                    1
                }
            }
        };

        public object GetInitialValueForConfigurableParameter(string configurableParameter)
        {
            throw new NotImplementedException();
        }

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<AtomicConstraintExpression> GetUnsatisfiedConstraintsFromEvaluation(ConstraintExpression constraintExpression, object propertyValue)
        {
            throw new NotImplementedException();
        }

        public string GetValueAsCultureInvariantString(object value) {
            throw new NotImplementedException();
        }

        public object GetValueFromSimulationParameter(IInstance fmuInstance, IVariable parameter)
        {
            throw new NotImplementedException();
        }

        public int IncreaseComp(object comparingValue, object targetValue)
        {
            throw new NotImplementedException();
        }

        public bool IsGreaterThanOrEqualTo(object comparingValue, object targetValue)
        {
            throw new NotImplementedException();
        }

        public bool IsLessThanOrEqualTo(object comparingValue, object targetValue)
        {
            throw new NotImplementedException();
        }

        public void WriteValueToSimulationParameter(IInstance fmuInstance, IVariable parameter, object value)
        {
            throw new NotImplementedException();
        }
    }
}
