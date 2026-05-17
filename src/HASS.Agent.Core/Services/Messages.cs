using CommunityToolkit.Mvvm.Messaging.Messages;
using HASS.Agent.Core.Enums;
using HASS.Agent.Core.Mqtt;
using HASS.Agent.Shared.Enums;

namespace HASS.Agent.Core.Services
{
    /// <summary>Sent when a component's status changes (replaces Variables.MainForm?.SetXxxStatus calls).</summary>
    public sealed class ComponentStatusMessage : ValueChangedMessage<(Component Component, HASS.Agent.Shared.Enums.ComponentStatus Status)>
    {
        public ComponentStatusMessage(Component component, HASS.Agent.Shared.Enums.ComponentStatus status)
            : base((component, status)) { }
    }

    /// <summary>Sent when the MQTT connection state changes.</summary>
    public sealed class MqttConnectionStateMessage : ValueChangedMessage<MqttConnectionState>
    {
        public MqttConnectionStateMessage(MqttConnectionState state) : base(state) { }
    }

    /// <summary>Sent when the global quick-actions hotkey is pressed.</summary>
    public sealed class QuickActionsHotkeyMessage { }

    /// <summary>Sent when an individual quick-action hotkey fires.</summary>
    public sealed class QuickActionHotkeyMessage : ValueChangedMessage<string>
    {
        public QuickActionHotkeyMessage(string hotkey) : base(hotkey) { }
    }

    /// <summary>Sent when an MQTT entity is discovered/registered in Home Assistant.</summary>
    public sealed class EntityDiscoveredMessage : ValueChangedMessage<(string EntityId, string Domain)>
    {
        public EntityDiscoveredMessage(string entityId, string domain)
            : base((entityId, domain)) { }
    }

    /// <summary>Sent when settings are saved so components can reload.</summary>
    public sealed class SettingsChangedMessage { }

    // ── MQTT payload messages (routed from MqttService) ───────────────────────

    public sealed class MqttNotificationMessage : ValueChangedMessage<string>
    {
        public MqttNotificationMessage(string payload) : base(payload) { }
    }

    public sealed class MqttMediaCommandMessage : ValueChangedMessage<string>
    {
        public MqttMediaCommandMessage(string payload) : base(payload) { }
    }

    public sealed class MqttCommandMessage : ValueChangedMessage<(string Topic, string Payload)>
    {
        public MqttCommandMessage(string topic, string payload) : base((topic, payload)) { }
    }
}
