using System.Reflection;
using HASS.Agent.Shared;
using HASS.Agent.Shared.Enums;
using HASS.Agent.Shared.HomeAssistant.Commands;
using HASS.Agent.Shared.Models.HomeAssistant;
using Serilog;

namespace HASS.Agent.Core.Entities;

/// <summary>
/// Reflectively instantiates concrete sensor/command classes from HASS.Agent.Shared
/// using the field values collected in the Add/Edit dialog.
/// </summary>
public static class EntityFactory
{
    private static readonly Assembly SharedAsm = typeof(AgentSharedBase).Assembly;

    /// <summary>
    /// Build an AbstractSingleValueSensor or AbstractMultiValueSensor from a registry entry + form values.
    /// Returns null if instantiation fails — caller should treat as validation error.
    /// </summary>
    public static object? CreateSensor(EntityTypeMetadata meta, string name, int updateInterval,
        IReadOnlyDictionary<string, string> fieldValues, string? existingId = null)
    {
        try
        {
            var type = SharedAsm.GetType(meta.FullTypeName)
                ?? throw new InvalidOperationException($"Sensor type {meta.FullTypeName} not found in HASS.Agent.Shared");

            var ctor = type.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .First();

            var args = ctor.GetParameters().Select(p => MapSensorArg(p, name, updateInterval, fieldValues, existingId)).ToArray();
            return ctor.Invoke(args);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ENTITY] Failed to instantiate sensor {key}: {err}", meta.Key, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Build an AbstractCommand from a registry entry + form values.
    /// </summary>
    public static AbstractCommand? CreateCommand(EntityTypeMetadata meta, string name,
        CommandEntityType entityType,
        IReadOnlyDictionary<string, string> fieldValues, string? existingId = null)
    {
        try
        {
            var type = SharedAsm.GetType(meta.FullTypeName)
                ?? throw new InvalidOperationException($"Command type {meta.FullTypeName} not found in HASS.Agent.Shared");

            var ctor = type.GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .First();

            var args = ctor.GetParameters().Select(p => MapCommandArg(p, name, entityType, fieldValues, existingId)).ToArray();
            return (AbstractCommand?)ctor.Invoke(args);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ENTITY] Failed to instantiate command {key}: {err}", meta.Key, ex.Message);
            return null;
        }
    }

    // ── Constructor-arg mapping ───────────────────────────────────────────────

    private static object? MapSensorArg(ParameterInfo p, string name, int interval,
        IReadOnlyDictionary<string, string> fields, string? id)
    {
        // Stock parameters every sensor has.
        if (p.Name == "name") return name;
        if (p.Name == "id") return id ?? Guid.NewGuid().ToString();
        if (p.Name == "updateInterval") return interval;
        if (p.Name is "deviceClass" or "icon" or "unitOfMeasurement" or "multiValueSensorName") return null;
        if (p.Name == "useAttributes" && p.ParameterType == typeof(bool)) return false;

        // Type-specific fields.
        return ConvertFieldValue(p, fields);
    }

    private static object? MapCommandArg(ParameterInfo p, string name, CommandEntityType entityType,
        IReadOnlyDictionary<string, string> fields, string? id)
    {
        if (p.Name == "name") return name;
        if (p.Name == "id") return id ?? Guid.NewGuid().ToString();
        if (p.Name == "entityType") return entityType;
        return ConvertFieldValue(p, fields);
    }

    private static object? ConvertFieldValue(ParameterInfo p, IReadOnlyDictionary<string, string> fields)
    {
        var key = p.Name ?? string.Empty;
        fields.TryGetValue(key, out var raw);
        raw ??= string.Empty;

        var t = p.ParameterType;
        if (t == typeof(string)) return raw;
        if (t == typeof(byte) || t == typeof(byte?))
            return byte.TryParse(raw, out var b) ? b : (byte)0;
        if (t == typeof(int) || t == typeof(int?))
            return int.TryParse(raw, out var i) ? i : 0;
        if (t == typeof(bool) || t == typeof(bool?))
            return bool.TryParse(raw, out var bo) && bo;
        if (t == typeof(List<byte>))
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                      .Select(s => byte.TryParse(s, out var v) ? v : (byte)0).ToList();
        // Fallback — leave it for the runtime to barf if we miss something.
        return null;
    }
}
