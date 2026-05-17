using CommunityToolkit.Mvvm.Messaging;
using HADotNet.Core;
using HADotNet.Core.Clients;
using HASS.Agent.Core.Enums;
using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Core.Services;
using HASS.Agent.Shared.Enums;
using Serilog;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;

namespace HASS.Agent.Core.HomeAssistant
{
    /// <summary>
    /// Injectable replacement for the original static HassApiManager.
    /// Connects to Home Assistant REST API, caches entity lists, executes actions.
    /// </summary>
    public class HassApiService : IHassApiService
    {
        private readonly IApplicationStateService _state;

        private ConfigClient? _configClient;
        private ServiceClient? _serviceClient;
        private EntityClient? _entityClient;
        private StatesClient? _statesClient;
        private EventClient? _eventClient;

        private static readonly string[] OnStates = { "on", "playing", "open", "opening" };

        public HassManagerStatus ManagerStatus { get; private set; } = HassManagerStatus.Initialising;
        public string HaVersion { get; private set; } = string.Empty;

        public List<string> AutomationList { get; private set; } = new();
        public List<string> ScriptList { get; private set; } = new();
        public List<string> InputBooleanList { get; private set; } = new();
        public List<string> SceneList { get; private set; } = new();
        public List<string> SwitchList { get; private set; } = new();
        public List<string> LightList { get; private set; } = new();
        public List<string> CoverList { get; private set; } = new();
        public List<string> ClimateList { get; private set; } = new();
        public List<string> MediaPlayerList { get; private set; } = new();

        public HassApiService(IApplicationStateService state)
        {
            _state = state;
        }

        public async Task<HassManagerStatus> InitializeAsync()
        {
            try
            {
                var settings = _state.AppSettings;

                if (string.IsNullOrWhiteSpace(settings.HassUri) || string.IsNullOrWhiteSpace(settings.HassToken))
                {
                    ManagerStatus = HassManagerStatus.ConfigMissing;
                    WeakReferenceMessenger.Default.Send(new ComponentStatusMessage(Component.HassApi, ComponentStatus.Stopped));
                    return ManagerStatus;
                }

                if (!InitializeClient())
                {
                    ManagerStatus = HassManagerStatus.Failed;
                    WeakReferenceMessenger.Default.Send(new ComponentStatusMessage(Component.HassApi, ComponentStatus.Failed));
                    return ManagerStatus;
                }

                // Retry until connected or shutting down
                while (!await GetConfigAsync())
                {
                    if (_state.ShuttingDown) return HassManagerStatus.Failed;
                    WeakReferenceMessenger.Default.Send(new ComponentStatusMessage(Component.HassApi, ComponentStatus.Connecting));
                    await Task.Delay(2000);
                }

                _serviceClient = ClientFactory.GetClient<ServiceClient>();
                _entityClient = ClientFactory.GetClient<EntityClient>();
                _statesClient = ClientFactory.GetClient<StatesClient>();
                _eventClient = ClientFactory.GetClient<EventClient>();

                ManagerStatus = HassManagerStatus.LoadingData;
                await LoadEntitiesAsync();

                ManagerStatus = HassManagerStatus.Ready;
                WeakReferenceMessenger.Default.Send(new ComponentStatusMessage(Component.HassApi, ComponentStatus.Ok));

                _ = Task.Run(PeriodicEntityReload);

                Log.Information("[HASS_API] Connected to {uri}, HA version {ver}", settings.HassUri, HaVersion);
                return ManagerStatus;
            }
            catch (Exception ex)
            {
                ManagerStatus = HassManagerStatus.Failed;
                Log.Fatal(ex, "[HASS_API] Fatal error during init: {err}", ex.Message);
                WeakReferenceMessenger.Default.Send(new ComponentStatusMessage(Component.HassApi, ComponentStatus.Failed));
                return ManagerStatus;
            }
        }

        public async Task<bool> ProcessQuickActionAsync(QuickAction action)
        {
            try
            {
                if (_serviceClient == null || _statesClient == null)
                {
                    Log.Warning("[HASS_API] Service client not ready for quick action");
                    return false;
                }

                var entityId = action.Entity;
                var hassAction = action.Action;

                // Resolve toggle to actual on/off based on current state
                if (hassAction == HassAction.Toggle)
                {
                    var state = await _statesClient.GetState(entityId);
                    hassAction = OnStates.Contains(state?.State) ? HassAction.Off : HassAction.On;
                }

                // Map HassDomain + HassAction → HA service name
                string serviceAction;
                var domain = action.Domain.ToString().ToLower();

                if (action.Domain == HassDomain.Cover)
                {
                    serviceAction = hassAction switch
                    {
                        HassAction.On => "open_cover",
                        HassAction.Off => "close_cover",
                        HassAction.Stop => "stop_cover",
                        _ => hassAction.ToString().ToLower()
                    };
                }
                else if (action.Domain == HassDomain.MediaPlayer)
                {
                    serviceAction = hassAction switch
                    {
                        HassAction.On => "media_play",
                        HassAction.Off => "media_stop",
                        _ => hassAction.ToString().ToLower()
                    };
                }
                else
                {
                    serviceAction = hassAction switch
                    {
                        HassAction.On => "turn_on",
                        HassAction.Off => "turn_off",
                        _ => hassAction.ToString().ToLower()
                    };
                }

                await _serviceClient.CallService(domain, serviceAction, new { entity_id = entityId });
                Log.Debug("[HASS_API] Executed {action} on {entity}", serviceAction, entityId);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HASS_API] Error executing quick action on {entity}: {err}", action.Entity, ex.Message);
                return false;
            }
        }

        public async Task FireEventAsync(string eventType, object? data = null)
        {
            try
            {
                if (_eventClient == null) return;
                await _eventClient.FireEvent(eventType, data);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HASS_API] Error firing event {type}: {err}", eventType, ex.Message);
            }
        }

        public async Task<bool> CheckConnectionAsync() => await GetConfigAsync();

        // ── Private helpers ────────────────────────────────────────────────────

        private bool InitializeClient()
        {
            try
            {
                var settings = _state.AppSettings;
                var handler = new HttpClientHandler();

                if (settings.HassAllowUntrustedCertificates)
                {
                    handler.CheckCertificateRevocationList = false;
                    handler.ServerCertificateCustomValidationCallback += (_, _, _, _) => true;
                }
                else if (!string.IsNullOrWhiteSpace(settings.HassClientCertificate))
                {
                    var cert = new X509Certificate2(settings.HassClientCertificate);
                    handler.ClientCertificates.Add(cert);
                }

                ClientFactory.Initialize(new Uri(settings.HassUri), settings.HassToken, handler);

                _configClient = ClientFactory.GetClient<ConfigClient>();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HASS_API] Error initializing client: {err}", ex.Message);
                return false;
            }
        }

        private async Task<bool> GetConfigAsync()
        {
            try
            {
                if (_configClient == null) return false;
                var config = await _configClient.GetConfiguration();
                HaVersion = config?.Version ?? string.Empty;
                return !string.IsNullOrWhiteSpace(HaVersion);
            }
            catch { return false; }
        }

        private async Task LoadEntitiesAsync()
        {
            try
            {
                if (_statesClient == null) return;
                var states = await _statesClient.GetStates();
                if (states == null) return;

                AutomationList.Clear(); ScriptList.Clear(); InputBooleanList.Clear();
                SceneList.Clear(); SwitchList.Clear(); LightList.Clear();
                CoverList.Clear(); ClimateList.Clear(); MediaPlayerList.Clear();

                foreach (var s in states)
                {
                    if (s.EntityId.StartsWith("automation.")) AutomationList.Add(s.EntityId);
                    else if (s.EntityId.StartsWith("script.")) ScriptList.Add(s.EntityId);
                    else if (s.EntityId.StartsWith("input_boolean.")) InputBooleanList.Add(s.EntityId);
                    else if (s.EntityId.StartsWith("scene.")) SceneList.Add(s.EntityId);
                    else if (s.EntityId.StartsWith("switch.")) SwitchList.Add(s.EntityId);
                    else if (s.EntityId.StartsWith("light.")) LightList.Add(s.EntityId);
                    else if (s.EntityId.StartsWith("cover.")) CoverList.Add(s.EntityId);
                    else if (s.EntityId.StartsWith("climate.")) ClimateList.Add(s.EntityId);
                    else if (s.EntityId.StartsWith("media_player.")) MediaPlayerList.Add(s.EntityId);
                }

                Log.Information("[HASS_API] Loaded {count} entities", states.Count);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[HASS_API] Error loading entities: {err}", ex.Message);
            }
        }

        private async Task PeriodicEntityReload()
        {
            while (!_state.ShuttingDown)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));
                if (!_state.ShuttingDown) await LoadEntitiesAsync();
            }
        }
    }
}
