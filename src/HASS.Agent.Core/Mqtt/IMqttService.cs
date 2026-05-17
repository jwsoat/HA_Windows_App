using HASS.Agent.Shared.Models.HomeAssistant;
using MQTTnet;

namespace HASS.Agent.Core.Mqtt
{
    public interface IMqttService
    {
        MqttConnectionState ConnectionState { get; }
        event EventHandler<MqttConnectionState>? ConnectionStateChanged;
        event EventHandler<(string EntityId, string Domain)>? EntityDiscovered;

        Task InitializeAsync();
        Task ReloadConfigurationAsync();
        Task DisconnectAsync();
        Task<bool> CheckConnectionAsync();

        Task PublishAsync(MqttApplicationMessage message);
        Task SubscribeAsync(AbstractCommand command);
        Task UnsubscribeAsync(AbstractCommand command);
        Task SubscribeNotificationsAsync();
        Task SubscribeMediaCommandsAsync();
        Task AnnounceAvailabilityAsync(bool online);
        Task AnnounceAutoDiscoveryAsync(AbstractDiscoverable discoverable, string domain);
    }
}
