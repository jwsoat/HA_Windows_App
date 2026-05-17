namespace HASS.Agent.Core.Mqtt
{
    public enum MqttConnectionState
    {
        NotConfigured,
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Error
    }
}
