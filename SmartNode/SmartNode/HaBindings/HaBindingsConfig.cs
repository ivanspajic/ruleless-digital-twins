using System.Text.Json.Serialization;

namespace SmartNode.HaBindings
{
    public enum HaSensorImpl
    {
        HomeAssistant,
        Constant,
        GeneralConstant,
        DummyEnergy,
        Fakepool
    }

    public enum HaActuatorImpl
    {
        HomeAssistant,
        DummyHeater,
        DummyFloorHeating,
        DummyDehumidifier
    }

    public class HaSensorBinding
    {
        public string SensorUri { get; set; } = string.Empty;
        public string ProcedureUri { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HaSensorImpl Kind { get; set; } = HaSensorImpl.HomeAssistant;

        public string? HaEntityId { get; set; }
        public string? Attribute { get; set; }

        public double? ConstantValue { get; set; }
    }

    public class HaActuatorBinding
    {
        public string ActuatorUri { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public HaActuatorImpl Kind { get; set; } = HaActuatorImpl.HomeAssistant;

        public string? HaEntityId { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Implementations.Actuators.HomeAssistant.HomeAssistantActuator.ActuatorKind? HaKind { get; set; }

        public string? OnOption { get; set; }
    }

    public class HaBindingsConfig
    {
        public string? Profile { get; set; }
        public string? Platform { get; set; }
        public List<HaSensorBinding> Sensors { get; set; } = new();
        public List<HaActuatorBinding> Actuators { get; set; } = new();
    }
}
