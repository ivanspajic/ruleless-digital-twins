using System.Text.Json.Serialization;
using Implementations.Actuators.HomeAssistant;

namespace SmartNode.HaBindings
{
    public class HaSensorBinding
    {
        public string SensorUri { get; set; } = string.Empty;
        public string ProcedureUri { get; set; } = string.Empty;
        public string HaEntityId { get; set; } = string.Empty;
        public string? Attribute { get; set; }
    }

    public class HaActuatorBinding
    {
        public string ActuatorUri { get; set; } = string.Empty;
        public string HaEntityId { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HomeAssistantActuator.ActuatorKind Kind { get; set; } = HomeAssistantActuator.ActuatorKind.Switch;

        public string? OnOption { get; set; }
    }

    public class HaBindingsConfig
    {
        public List<HaSensorBinding> Sensors { get; set; } = new();
        public List<HaActuatorBinding> Actuators { get; set; } = new();
    }
}
