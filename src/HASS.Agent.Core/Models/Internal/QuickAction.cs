using System.Diagnostics.CodeAnalysis;
using HASS.Agent.Core.Models.HomeAssistant;
using HASS.Agent.Shared.Enums;
using HASS.Agent.Shared.Models.HomeAssistant;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace HASS.Agent.Core.Models.Internal
{
    public static class QuickActionExtensions
    {
        public static HassEntity ToHassEntity(this QuickAction qa) => new(qa.Domain, qa.Entity);
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class QuickAction
    {
        // ── Original fields (JSON contract unchanged) ──────────────────────────

        public Guid Id { get; set; } = Guid.NewGuid();

        [JsonConverter(typeof(StringEnumConverter))]
        public HassDomain Domain { get; set; }

        public string Entity { get; set; } = string.Empty;

        [JsonConverter(typeof(StringEnumConverter))]
        public HassAction Action { get; set; }

        public bool HotKeyEnabled { get; set; }
        public string HotKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // ── New fields — all nullable/defaulted so existing configs load cleanly ──

        /// <summary>Group/room label shown in the overlay filter pills. Null = ungrouped.</summary>
        public string? GroupName { get; set; }

        /// <summary>Display order within the overlay grid.</summary>
        public int SortOrder { get; set; } = 0;

        /// <summary>When true, the overlay card shows the current HA entity state badge.</summary>
        public bool ShowEntityState { get; set; } = false;

        /// <summary>
        /// Multi-step sequence. When non-null and non-empty, the steps are executed in order
        /// instead of the single Domain/Entity/Action.
        /// </summary>
        public List<QuickActionSequenceStep>? Sequence { get; set; }

        /// <summary>Optional Jinja2-style condition; action is skipped when it evaluates to false.</summary>
        public string? TemplateCondition { get; set; }

        public bool IsEnabled { get; set; } = true;

        // ── Computed helpers ────────────────────────────────────────────────────

        [JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Description) ? Entity : Description;

        [JsonIgnore]
        public bool IsSequence => Sequence is { Count: > 0 };
    }
}
