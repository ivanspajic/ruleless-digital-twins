using Logic.Models.OntologicalModels;
using System.Diagnostics.CodeAnalysis;

namespace TestProject.Mocks.EqualityComparers
{
    internal class ActionEqualityComparer : IEqualityComparer<Logic.Models.OntologicalModels.Action>
    {
        public bool Equals(Logic.Models.OntologicalModels.Action? x, Logic.Models.OntologicalModels.Action? y)
        {
            if (x is ActuationAction && y is ActuationAction)
            {
                var xActuationAction = x as ActuationAction;
                var yActuationAction = y as ActuationAction;

                if (xActuationAction!.ActuatorState.Actuator.Equals(yActuationAction!.ActuatorState.Actuator) &&
                    xActuationAction.ActuatorState.Name.Equals(yActuationAction.ActuatorState.Name) &&
                    xActuationAction.Name.Equals(yActuationAction.Name) &&
                    xActuationAction.ActedOnProperty.Equals(yActuationAction.ActedOnProperty))
                {
                    return true;
                }
            }
            else if (x is ReconfigurationAction && y is ReconfigurationAction)
            {
                var xReconfigurationAction = x as ReconfigurationAction;
                var yReconfigurationAction = y as ReconfigurationAction;

                if (xReconfigurationAction!.Effect == yReconfigurationAction!.Effect &&
                    xReconfigurationAction.NewParameterValue.Equals(yReconfigurationAction.NewParameterValue) &&
                    xReconfigurationAction.Name.Equals(yReconfigurationAction.Name) &&
                    xReconfigurationAction.ConfigurableParameter.OwlType.Equals(yReconfigurationAction.ConfigurableParameter.OwlType) &&
                    xReconfigurationAction.ConfigurableParameter.Value.Equals(yReconfigurationAction.ConfigurableParameter.Value) &&
                    xReconfigurationAction.ConfigurableParameter.LowerLimitValue.Equals(yReconfigurationAction.ConfigurableParameter.LowerLimitValue) &&
                    xReconfigurationAction.ConfigurableParameter.UpperLimitValue.Equals(yReconfigurationAction.ConfigurableParameter.UpperLimitValue))
                {
                    return true;
                }
            }

            return false;
        }

        public int GetHashCode([DisallowNull] Logic.Models.OntologicalModels.Action obj)
        {
            if (obj is ActuationAction)
            {
                var objActuationAction = obj as ActuationAction;

                return objActuationAction!.Name.GetHashCode() * 
                    objActuationAction.ActedOnProperty.GetHashCode() * 
                    objActuationAction.ActuatorState.Name.GetHashCode() * 
                    objActuationAction.ActuatorState.Actuator.GetHashCode();
            }
            else
            {
                var objReconfigurationAction = obj as ReconfigurationAction;

                return objReconfigurationAction!.Name.GetHashCode() *
                    objReconfigurationAction.Effect.GetHashCode() *
                    objReconfigurationAction.NewParameterValue.GetHashCode() *
                    objReconfigurationAction.ConfigurableParameter.OwlType.GetHashCode() *
                    objReconfigurationAction.ConfigurableParameter.Value.GetHashCode() *
                    objReconfigurationAction.ConfigurableParameter.LowerLimitValue.GetHashCode() *
                    objReconfigurationAction.ConfigurableParameter.UpperLimitValue.GetHashCode();
            }
        }
    }
}
