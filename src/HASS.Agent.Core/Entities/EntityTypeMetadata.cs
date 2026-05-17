namespace HASS.Agent.Core.Entities;

/// <summary>
/// Describes a sensor or command type so the Add/Edit dialog can render fields and reflectively
/// instantiate the concrete class from HASS.Agent.Shared.
/// </summary>
public sealed class EntityTypeMetadata
{
    /// <summary>Short stable key (matches the class name in HASS.Agent.Shared).</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Human-readable name shown in pickers.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>One-line description shown next to the picker entry.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Grouping label for the picker ("System", "Hardware", "Media", "Custom"...).</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Fully-qualified type name in HASS.Agent.Shared (used by reflection at save time).</summary>
    public string FullTypeName { get; init; } = string.Empty;

    /// <summary>True if this is a multi-value sensor (e.g. AudioSensors, BatterySensors).</summary>
    public bool IsMultiValue { get; init; }

    /// <summary>Default refresh interval in seconds for sensors. Ignored for commands.</summary>
    public int DefaultIntervalSeconds { get; init; } = 30;

    /// <summary>Type-specific input fields beyond Name/Interval.</summary>
    public IReadOnlyList<EntityField> Fields { get; init; } = Array.Empty<EntityField>();
}

/// <summary>A single input field on an Add/Edit dialog beyond the standard Name/Interval.</summary>
public sealed class EntityField
{
    /// <summary>Matches the constructor parameter name in the concrete class.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>Label shown in the UI.</summary>
    public string Label { get; init; } = string.Empty;

    public string Placeholder { get; init; } = string.Empty;
    public string DefaultValue { get; init; } = string.Empty;
    public bool Required { get; init; } = true;

    /// <summary>What kind of input control to render.</summary>
    public EntityFieldKind Kind { get; init; } = EntityFieldKind.Text;
}

public enum EntityFieldKind
{
    Text,
    MultilineText,
    Number,
    Boolean,
    /// <summary>Comma-separated list of byte values (for MultipleKeysCommand).</summary>
    KeyCodeList
}
