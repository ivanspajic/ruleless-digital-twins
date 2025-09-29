using Logic.Models.OntologicalModels;
using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.Comparers
{
    internal class ActionEqualityComparer : IEqualityComparer<Models.OntologicalModels.Action>
    {
        public bool Equals(Models.OntologicalModels.Action? x, Models.OntologicalModels.Action? y)
        {
            if (x is ActuationAction && y is ActuationAction)
            {
                var xActuationAction = x as ActuationAction;
                var yActuationAction = y as ActuationAction;

                if (xActuationAction!.Actuator.Name.Equals(yActuationAction!.Actuator.Name) &&
                    xActuationAction.NewStateValue.Equals(yActuationAction.NewStateValue) &&
                    xActuationAction.Name.Equals(yActuationAction.Name))
                {
                    return true;
                }
            }
            else if (x is ReconfigurationAction && y is ReconfigurationAction)
            {
                var xReconfigurationAction = x as ReconfigurationAction;
                var yReconfigurationAction = y as ReconfigurationAction;

                if (xReconfigurationAction!.NewParameterValue.Equals(yReconfigurationAction!.NewParameterValue) &&
                    xReconfigurationAction.Name.Equals(yReconfigurationAction.Name) &&
                    xReconfigurationAction.ConfigurableParameter.OwlType.Equals(yReconfigurationAction.ConfigurableParameter.OwlType) &&
                    xReconfigurationAction.ConfigurableParameter.Value.Equals(yReconfigurationAction.ConfigurableParameter.Value))
                {
                    return true;
                }
            }

            return false;
        }

        public int GetHashCode([DisallowNull] Models.OntologicalModels.Action obj)
        {
            if (obj is ActuationAction)
            {
                var objActuationAction = obj as ActuationAction;

                return objActuationAction!.Name.GetHashCode() *
                    objActuationAction.Actuator.Name.GetHashCode() *
                    objActuationAction.NewStateValue.GetHashCode();
            }
            else
            {
                var objReconfigurationAction = obj as ReconfigurationAction;

                return objReconfigurationAction!.Name.GetHashCode() *
                    objReconfigurationAction.NewParameterValue.GetHashCode() *
                    objReconfigurationAction.ConfigurableParameter.OwlType.GetHashCode() *
                    objReconfigurationAction.ConfigurableParameter.Value.GetHashCode();
            }
        }
    }
}
