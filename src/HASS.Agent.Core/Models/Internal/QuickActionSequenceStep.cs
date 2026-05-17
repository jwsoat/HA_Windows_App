using HASS.Agent.Shared.Enums;
using HASS.Agent.Shared.Models.HomeAssistant;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace HASS.Agent.Core.Models.Internal
{
    public class QuickActionSequenceStep
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public HassDomain Domain { get; set; }

        public string Entity { get; set; } = string.Empty;

        [JsonConverter(typeof(StringEnumConverter))]
        public HassAction Action { get; set; }

        /// <summary>Milliseconds to wait after executing this step before the next one.</summary>
        public int DelayAfterMs { get; set; } = 0;
    }
}
