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

        public object GetObservablePropertyValueFromMeasuredPropertyValues(params object[] measuredPropertyValues)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetPossibleValuesForReconfigurationAction(object currentValue, object minimumValue, object maximumValue, int simulationGranularity, Effect effect, string configurableParameterName)
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

        public bool IsGreaterThan(object comparingValue, object targetValue)
        {
            throw new NotImplementedException();
        }

        public bool IsLessThan(object comparingValue, object targetValue)
        {
            throw new NotImplementedException();
        }
    }
}
