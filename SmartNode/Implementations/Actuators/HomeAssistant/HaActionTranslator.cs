namespace Implementations.Actuators.HomeAssistant {

    /// <summary>
    /// Translates an RDT actuator state (as emitted by the hass-to-rdt exporter)
    /// into a concrete Home Assistant service call plus its JSON payload.
    ///
    /// Without this, a state like "on" would call /api/services/light/on, which
    /// Home Assistant rejects — it expects /api/services/light/turn_on. Likewise
    /// structured states ("pct:50", "preset:away", "fan:low", "swing:vertical")
    /// must become a service with the matching data field, not part of the path.
    /// </summary>
    public static class HaActionTranslator {

        public readonly record struct HaCall(string Service, IReadOnlyDictionary<string, object> Data);

        /// <param name="domain">HA domain derived from the entity id (e.g. "light").</param>
        /// <param name="entityId">Full HA entity id (e.g. "light.kitchen").</param>
        /// <param name="state">RDT actuator state to apply.</param>
        public static HaCall Translate(string domain, string entityId, string state) {
            var data = new Dictionary<string, object> { ["entity_id"] = entityId };
            string s = state ?? string.Empty;

            // Structured states: "<field>:<value>"
            if (s.StartsWith("pct:") && int.TryParse(s[4..], out int pct)) {
                data["percentage"] = pct;
                return new HaCall("set_percentage", data);
            }
            if (s.StartsWith("preset:")) {
                data["preset_mode"] = s["preset:".Length..];
                return new HaCall("set_preset_mode", data);
            }
            if (s.StartsWith("fan:")) {
                data["fan_mode"] = s["fan:".Length..];
                return new HaCall("set_fan_mode", data);
            }
            if (s.StartsWith("swing:")) {
                data["swing_mode"] = s["swing:".Length..];
                return new HaCall("set_swing_mode", data);
            }

            // Toggle states shared across domains.
            switch (s) {
                case "on":       return new HaCall("turn_on", data);
                case "off":      return new HaCall("turn_off", data);
                case "locked":   return new HaCall("lock", data);
                case "unlocked": return new HaCall("unlock", data);
            }

            // Bare states that are domain-specific.
            switch (domain) {
                case "input_select":
                    data["option"] = s;
                    return new HaCall("select_option", data);
                case "climate":
                    data["hvac_mode"] = s;
                    return new HaCall("set_hvac_mode", data);
            }

            // Fallback: treat the state as the service name itself.
            return new HaCall(s, data);
        }
    }
}
