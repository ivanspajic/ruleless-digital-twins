using Femyou;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using TestProject.Mocks.EqualityComparers;

namespace TestProject.Mocks
{
    internal class DoubleValueHandlerMock : IValueHandler
    {
        private Dictionary<(ConstraintExpression, object), IEnumerable<AtomicConstraintExpression>> _unsatisfiedConstraints = new(new ConstraintExpressionObjectTupleEqualityComparer())
        {
            { 
                (new AtomicConstraintExpression
                {
                    ConstraintType = ConstraintType.GreaterThan,
                    Right = "10.1"
                },
                1.02),
                new List<AtomicConstraintExpression>
                {
                    new AtomicConstraintExpression
                    {
                        ConstraintType = ConstraintType.GreaterThan,
                        Right = "10.1"
                    }
                }
            },
            { 
                (new AtomicConstraintExpression
                {
                    ConstraintType = ConstraintType.LessThanOrEqualTo,
                    Right = "28.5"
                },
                1.02),
                new List<AtomicConstraintExpression> { }
            },
            {
                (new AtomicConstraintExpression
                {
                    ConstraintType = ConstraintType.GreaterThanOrEqualTo,
                    Right = "27.5"
                },
                1.02),
                new List<AtomicConstraintExpression>
                {
                    new AtomicConstraintExpression
                    {
                        ConstraintType = ConstraintType.GreaterThanOrEqualTo,
                        Right = "27.5"
                    }
                }
            },
            {
                (new AtomicConstraintExpression
                {
                    ConstraintType = ConstraintType.LessThan,
                    Right = "30.2"
                },
                1.02),
                new List<AtomicConstraintExpression> { }
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
            if (_unsatisfiedConstraints.TryGetValue((constraintExpression, propertyValue), out IEnumerable<AtomicConstraintExpression> unsatisfiedConstraints))
            {
                return unsatisfiedConstraints;
            }

            return null!;
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
