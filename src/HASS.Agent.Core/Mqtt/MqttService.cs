using System.Security.Cryptography.X509Certificates;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using HASS.Agent.Core.Services;
using HASS.Agent.Shared.Enums;
using HASS.Agent.Shared.HomeAssistant.Commands;
using HASS.Agent.Shared.Models.HomeAssistant;
using HASS.Agent.Shared.Mqtt;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using Serilog;
using MqttTopicFilter = MQTTnet.Packets.MqttTopicFilter;

namespace HASS.Agent.Core.Mqtt
{
    /// <summary>
    /// MQTT connectivity — MQTTnet v4, state machine, credential vault, HA autodiscovery.
    /// Replaces the original MqttManager with a fully injectable, UI-free service.
    /// Also implements <see cref="IMqttManager"/> so the HASS.Agent.Shared sensor and command
    /// classes (which call Variables.MqttManager statically) can publish through us.
    /// </summary>
    public class MqttService : IMqttService, IMqttManager, IDisposable
    {
        private readonly IApplicationStateService _state;
        private readonly MqttCredentialVault _vault;

        private IManagedMqttClient? _client;
        private MqttConnectionState _connectionState = MqttConnectionState.NotConfigured;

        public MqttConnectionState ConnectionState
        {
            get => _connectionState;
            private set
            {
                if (_connectionState == value) return;
                _connectionState = value;
                ConnectionStateChanged?.Invoke(this, value);
                WeakReferenceMessenger.Default.Send(new MqttConnectionStateMessage(value));
            }
        }

        public event EventHandler<MqttConnectionState>? ConnectionStateChanged;
        public event EventHandler<(string EntityId, string Domain)>? EntityDiscovered;

        public MqttService(IApplicationStateService state, MqttCredentialVault vault)
        {
            _state = state;
            _vault = vault;
        }

        public async Task InitializeAsync()
        {
            var settings = _state.AppSettings;

            if (!settings.MqttEnabled)
            {
                ConnectionState = MqttConnectionState.NotConfigured;
                Log.Information("[MQTT] Disabled in settings");
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.MqttAddress))
            {
                ConnectionState = MqttConnectionState.NotConfigured;
                Log.Warning("[MQTT] No broker address configured");
                return;
            }

            // Migrate plaintext password from JSON → vault
            if (!string.IsNullOrWhiteSpace(settings.MqttPassword))
            {
                _vault.MigrateFromPlaintext(settings.MqttUsername, settings.MqttPassword);
                settings.MqttPassword = string.Empty;
            }

            await ConnectAsync();
        }

        public async Task ReloadConfigurationAsync()
        {
            await DisconnectAsync();
            await InitializeAsync();
        }

        public async Task DisconnectAsync()
        {
            if (_client == null) return;
            try
            {
                await _client.StopAsync();
                _client.Dispose();
                _client = null;
                ConnectionState = MqttConnectionState.Disconnected;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MQTT] Error during disconnect: {err}", ex.Message);
            }
        }

        public Task<bool> CheckConnectionAsync() =>
            Task.FromResult(_client?.IsConnected ?? false);

        public async Task PublishAsync(MqttApplicationMessage message)
        {
            if (_client == null || !_client.IsConnected) return;
            try { await _client.EnqueueAsync(message); }
            catch (Exception ex) { Log.Error(ex, "[MQTT] Publish error: {err}", ex.Message); }
        }

        public async Task SubscribeAsync(AbstractCommand command)
        {
            if (_client == null) return;
            try
            {
                var config = (CommandDiscoveryConfigModel)command.GetAutoDiscoveryConfig();
                var filters = new List<MqttTopicFilter> { new MqttTopicFilterBuilder().WithTopic(config.Command_topic).Build() };
                if (!string.IsNullOrWhiteSpace(config.Action_topic))
                    filters.Add(new MqttTopicFilterBuilder().WithTopic(config.Action_topic).Build());
                await _client.SubscribeAsync(filters);
            }
            catch (Exception ex) { Log.Error(ex, "[MQTT] Subscribe error for command {name}: {err}", command.Name, ex.Message); }
        }

        public async Task UnsubscribeAsync(AbstractCommand command)
        {
            if (_client == null) return;
            try
            {
                var config = (CommandDiscoveryConfigModel)command.GetAutoDiscoveryConfig();
                await _client.UnsubscribeAsync(config.Command_topic);
                if (!string.IsNullOrWhiteSpace(config.Action_topic))
                    await _client.UnsubscribeAsync(config.Action_topic);
            }
            catch (Exception ex) { Log.Error(ex, "[MQTT] Unsubscribe error: {err}", ex.Message); }
        }

        public async Task SubscribeNotificationsAsync()
        {
            if (_client == null) return;
            var topic = $"hass.agent/notifications/{_state.AppSettings.DeviceName}";
            await _client.SubscribeAsync(new[] { new MqttTopicFilterBuilder().WithTopic(topic).Build() });
            Log.Information("[MQTT] Subscribed to notifications on {topic}", topic);
        }

        public async Task SubscribeMediaCommandsAsync()
        {
            if (_client == null) return;
            var topic = $"hass.agent/media_player/{_state.AppSettings.DeviceName}/cmd";
            await _client.SubscribeAsync(new[] { new MqttTopicFilterBuilder().WithTopic(topic).Build() });
            Log.Information("[MQTT] Subscribed to media commands on {topic}", topic);
        }

        public async Task AnnounceAvailabilityAsync(bool online)
        {
            var topic = $"{_state.AppSettings.MqttDiscoveryPrefix}/sensor/{_state.AppSettings.DeviceName}/availability";
            var payload = online ? "online" : "offline";
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithRetainFlag(true)
                .Build();
            await PublishAsync(msg);
        }

        public Task AnnounceAutoDiscoveryAsync(AbstractDiscoverable discoverable, string domain) =>
            AnnounceAutoDiscoveryAsync(discoverable, domain, clearConfig: false, migration: false);

        public async Task AnnounceAutoDiscoveryAsync(AbstractDiscoverable discoverable, string domain,
            bool clearConfig, bool migration)
        {
            try
            {
                var deviceName = _state.DeviceConfig?.Name ?? _state.AppSettings.DeviceName;
                var topic = $"{_state.AppSettings.MqttDiscoveryPrefix}/{domain}/{deviceName}/{discoverable.ObjectId}/config";

                byte[] payload;
                if (clearConfig)
                {
                    // Empty payload removes the entity from HA (or signals a migration when paired with the migrate flag).
                    payload = migration
                        ? System.Text.Encoding.UTF8.GetBytes("{\"migrate_discovery\": true }")
                        : Array.Empty<byte>();
                }
                else
                {
                    var configObj = discoverable.GetAutoDiscoveryConfig();
                    var json = System.Text.Json.JsonSerializer.Serialize(configObj, configObj.GetType());
                    payload = System.Text.Encoding.UTF8.GetBytes(json);
                }

                var msg = new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithPayload(payload)
                    .WithRetainFlag(_state.AppSettings.MqttUseRetainFlag)
                    .Build();

                await PublishAsync(msg);
                if (!clearConfig)
                    EntityDiscovered?.Invoke(this, (discoverable.ObjectId, domain));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MQTT] AutoDiscovery error for {name}: {err}", discoverable.Name, ex.Message);
            }
        }

        // ── Republish all autodiscovery configs after (re)connect ────────────────

        private async Task RepublishAllDiscoveryAsync()
        {
            try
            {
                foreach (var sv in _state.SingleValueSensors.ToList())
                    await AnnounceAutoDiscoveryAsync(sv, "sensor");

                foreach (var mv in _state.MultiValueSensors.ToList())
                    await AnnounceAutoDiscoveryAsync(mv, "sensor");

                foreach (var cmd in _state.Commands.ToList())
                {
                    // Commands map to several HA domains depending on EntityType
                    var domain = cmd.EntityType switch
                    {
                        Shared.Enums.CommandEntityType.Switch => "switch",
                        Shared.Enums.CommandEntityType.Lock   => "lock",
                        Shared.Enums.CommandEntityType.Light  => "light",
                        _                                     => "button"
                    };
                    await AnnounceAutoDiscoveryAsync(cmd, domain);
                    await SubscribeAsync(cmd);
                }

                Log.Information("[MQTT] Republished discovery for {sv}+{mv} sensors and {cmd} commands",
                    _state.SingleValueSensors.Count, _state.MultiValueSensors.Count, _state.Commands.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[MQTT] Republish discovery failed: {err}", ex.Message);
            }
        }

        // ── IMqttManager (bridge for HASS.Agent.Shared sensor/command classes) ────
        // These map our async/state-machine API onto the surface that Variables.MqttManager
        // expects, so concrete sensors/commands can publish autodiscovery + state through us.

        bool IMqttManager.IsConnected() => _client?.IsConnected ?? false;
        bool IMqttManager.IsReady() => _client?.IsConnected ?? false;
        void IMqttManager.Initialize() => _ = Task.Run(InitializeAsync);
        void IMqttManager.ReloadConfiguration() => _ = Task.Run(ReloadConfigurationAsync);
        void IMqttManager.Disconnect() => _ = Task.Run(DisconnectAsync);

        bool IMqttManager.UseRetainFlag() => _state.AppSettings.MqttUseRetainFlag;
        string IMqttManager.MqttDiscoveryPrefix() => _state.AppSettings.MqttDiscoveryPrefix;

        MqttStatus IMqttManager.GetStatus() => ConnectionState switch
        {
            MqttConnectionState.NotConfigured => MqttStatus.ConfigMissing,
            MqttConnectionState.Connected     => MqttStatus.Connected,
            MqttConnectionState.Connecting    => MqttStatus.Connecting,
            MqttConnectionState.Reconnecting  => MqttStatus.Connecting,
            MqttConnectionState.Disconnected  => MqttStatus.Disconnected,
            MqttConnectionState.Error         => MqttStatus.Error,
            _                                 => MqttStatus.Disconnected
        };

        DeviceConfigModel IMqttManager.GetDeviceConfigModel() => _state.DeviceConfig ?? CreateDeviceConfigModelInternal();

        void IMqttManager.CreateDeviceConfigModel()
        {
            _state.DeviceConfig = CreateDeviceConfigModelInternal();
        }

        Task<bool> IMqttManager.PublishAsync(MqttApplicationMessage message) =>
            PublishWithResultAsync(message);

        Task IMqttManager.AnnounceAutoDiscoveryConfigAsync(AbstractDiscoverable discoverable, string domain,
            bool clearConfig = false, bool migration = false)
            => AnnounceAutoDiscoveryAsync(discoverable, domain, clearConfig, migration);

        Task IMqttManager.AnnounceAvailabilityAsync(bool offline = false) => AnnounceAvailabilityAsync(!offline);

        Task IMqttManager.ClearDeviceConfigAsync() => Task.CompletedTask;
        Task IMqttManager.SubscribeAsync(AbstractCommand command) => SubscribeAsync(command);
        Task IMqttManager.UnsubscribeAsync(AbstractCommand command) => UnsubscribeAsync(command);
        Task IMqttManager.SubscribeNotificationsAsync() => SubscribeNotificationsAsync();
        Task IMqttManager.SubscribeMediaCommandsAsync() => SubscribeMediaCommandsAsync();

        private async Task<bool> PublishWithResultAsync(MqttApplicationMessage msg)
        {
            try { await PublishAsync(msg); return _client?.IsConnected ?? false; }
            catch { return false; }
        }

        private DeviceConfigModel CreateDeviceConfigModelInternal()
        {
            var deviceName = _state.AppSettings.DeviceName;
            return new DeviceConfigModel
            {
                Name = deviceName,
                Identifiers = $"hass.agent-{deviceName}",
                Manufacturer = "LAB02 Research",
                Model = "HASS.Agent",
                Sw_version = typeof(MqttService).Assembly.GetName().Version?.ToString() ?? "unknown"
            };
        }

        // ── Private connection logic ──────────────────────────────────────────

        private async Task ConnectAsync()
        {
            try
            {
                ConnectionState = MqttConnectionState.Connecting;

                var settings = _state.AppSettings;
                var factory = new MqttFactory();
                _client = factory.CreateManagedMqttClient();

                _client.ConnectedAsync += async e =>
                {
                    ConnectionState = MqttConnectionState.Connected;
                    Log.Information("[MQTT] Connected to {host}:{port}", settings.MqttAddress, settings.MqttPort);
                    await AnnounceAvailabilityAsync(true);
                    // Subscribe to the HA-Integration's command topics so notifications and media control
                    // from Home Assistant can reach Windows.
                    await SubscribeNotificationsAsync();
                    await SubscribeMediaCommandsAsync();
                    // Republish all sensor/command autodiscovery configs after every (re)connect so HA
                    // doesn't lose them when the broker bounces. Mirrors hass-agent/HASS.Agent PR #230.
                    await RepublishAllDiscoveryAsync();
                };

                _client.DisconnectedAsync += async e =>
                {
                    if (_state.ShuttingDown)
                    {
                        ConnectionState = MqttConnectionState.Disconnected;
                        return;
                    }
                    ConnectionState = MqttConnectionState.Reconnecting;
                    Log.Warning("[MQTT] Disconnected — managed client will reconnect");
                    await Task.CompletedTask;
                };

                _client.ConnectingFailedAsync += async e =>
                {
                    ConnectionState = MqttConnectionState.Error;
                    Log.Error(e.Exception, "[MQTT] Connection failed: {err}", e.Exception?.Message);
                    await Task.CompletedTask;
                };

                _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

                var clientId = string.IsNullOrWhiteSpace(settings.MqttClientId)
                    ? $"hass-agent-{Guid.NewGuid():N}"
                    : settings.MqttClientId;

                var optionsBuilder = new MqttClientOptionsBuilder()
                    .WithClientId(clientId)
                    .WithTcpServer(settings.MqttAddress, settings.MqttPort)
                    .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
                    .WithWillTopic($"{settings.MqttDiscoveryPrefix}/sensor/{settings.DeviceName}/availability")
                    .WithWillPayload(Encoding.UTF8.GetBytes("offline"))
                    .WithWillRetain(true);

                // credentials from vault
                if (!string.IsNullOrWhiteSpace(settings.MqttUsername))
                {
                    var (user, pass) = _vault.Retrieve(settings.MqttUsername);
                    optionsBuilder.WithCredentials(user, pass);
                }

                // TLS
                if (settings.MqttUseTls)
                {
                    optionsBuilder.WithTlsOptions(tls =>
                    {
                        tls.UseTls(true);

                        if (settings.MqttAllowUntrustedCertificates)
                        {
                            tls.WithCertificateValidationHandler(_ => true);
                        }
                        else if (!string.IsNullOrWhiteSpace(settings.MqttRootCertificate))
                        {
                            // Pin to a specific root certificate via custom validation
                            var rootCert = new X509Certificate2(settings.MqttRootCertificate);
                            tls.WithCertificateValidationHandler(ctx =>
                                ctx.Chain?.ChainElements?.Cast<X509ChainElement>()
                                   .Any(e => e.Certificate.Thumbprint == rootCert.Thumbprint) == true);
                        }
                    });
                }

                var managedOptions = new ManagedMqttClientOptionsBuilder()
                    .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                    .WithClientOptions(optionsBuilder.Build())
                    .Build();

                await _client.StartAsync(managedOptions);
            }
            catch (Exception ex)
            {
                ConnectionState = MqttConnectionState.Error;
                Log.Fatal(ex, "[MQTT] Fatal error during connect: {err}", ex.Message);
            }
        }

        private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.PayloadSegment.Count > 0
                ? Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment)
                : string.Empty;

            Log.Debug("[MQTT] Received message on {topic}", topic);

            // Route to appropriate handler — the managers subscribe via their own handlers
            // This service just logs; managers register their own event handlers on subscription.
            _ = Task.Run(() => RouteMessage(topic, payload));
            return Task.CompletedTask;
        }

        private void RouteMessage(string topic, string payload)
        {
            var deviceName = _state.AppSettings.DeviceName;

            // Notification topic
            if (topic == $"hass.agent/notifications/{deviceName}")
            {
                WeakReferenceMessenger.Default.Send(new MqttNotificationMessage(payload));
                return;
            }

            // Media command topic
            if (topic == $"hass.agent/media_player/{deviceName}/cmd")
            {
                WeakReferenceMessenger.Default.Send(new MqttMediaCommandMessage(payload));
                return;
            }

            // Command topics — broadcast for CommandsManager to handle
            WeakReferenceMessenger.Default.Send(new MqttCommandMessage(topic, payload));
        }

        public void Dispose()
        {
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
