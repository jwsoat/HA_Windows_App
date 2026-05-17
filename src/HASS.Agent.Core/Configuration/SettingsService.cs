using HASS.Agent.Core.Enums;
using HASS.Agent.Core.Models.Config;
using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Core.Services;
using HASS.Agent.Shared;
using HASS.Agent.Shared.Models.HomeAssistant;
using Microsoft.Win32;
using Newtonsoft.Json;
using Serilog;

namespace HASS.Agent.Core.Configuration
{
    public class SettingsService : ISettingsService
    {
        private readonly IApplicationStateService _state;

        public SettingsService(IApplicationStateService state)
        {
            _state = state;
        }

        public async Task<bool> LoadAsync()
        {
            Log.Information("[SETTINGS] Config path: {path}", _state.ConfigPath);

            // Try migrating from a legacy LAB02 install before deciding we're a fresh start.
            LegacyConfigMigrator.MigrateIfNeeded(_state.ConfigPath);

            if (!Directory.Exists(_state.ConfigPath))
            {
                Directory.CreateDirectory(_state.ConfigPath);
                StoreInitialDefaults();
                return true;
            }

            return await Task.Run(LoadAppSettings);
        }

        public async Task<bool> LoadEntitiesAsync()
        {
            var ok = true;

            ok &= await Task.Run(LoadQuickActions);
            ok &= await Task.Run(LoadCommands);
            ok &= await Task.Run(LoadSensors);

            return ok;
        }

        public bool StoreAppSettings()
        {
            try
            {
                Directory.CreateDirectory(_state.ConfigPath);
                var json = JsonConvert.SerializeObject(_state.AppSettings, Formatting.Indented);
                File.WriteAllText(_state.AppSettingsFile, json);
                Log.Information("[SETTINGS] App settings stored");
                return true;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[SETTINGS] Error storing app settings: {err}", ex.Message);
                return false;
            }
        }

        public bool StoreQuickActions()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_state.QuickActions, Formatting.Indented);
                File.WriteAllText(_state.QuickActionsFile, json);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SETTINGS] Error storing quick actions: {err}", ex.Message);
                return false;
            }
        }

        public bool StoreCommands()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_state.Commands, Formatting.Indented,
                    new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto });
                File.WriteAllText(_state.CommandsFile, json);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SETTINGS] Error storing commands: {err}", ex.Message);
                return false;
            }
        }

        public bool StoreSensors()
        {
            try
            {
                var jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
                var single = JsonConvert.SerializeObject(_state.SingleValueSensors, Formatting.Indented, jsonSettings);
                var multi = JsonConvert.SerializeObject(_state.MultiValueSensors, Formatting.Indented, jsonSettings);

                // sensors.json stores both in a wrapper
                var wrapper = new { SingleValue = JsonConvert.DeserializeObject(single), MultiValue = JsonConvert.DeserializeObject(multi) };
                File.WriteAllText(_state.SensorsFile, JsonConvert.SerializeObject(wrapper, Formatting.Indented));
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SETTINGS] Error storing sensors: {err}", ex.Message);
                return false;
            }
        }

        public bool Store()
        {
            var ok = StoreAppSettings();
            ok &= StoreQuickActions();
            ok &= StoreCommands();
            ok &= StoreSensors();
            return ok;
        }

        public bool GetExtendedLoggingSetting()
        {
            try
            {
                var val = Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\LAB02Research\HASSAgent", "ExtendedLogging", "0") as string;
                return val == "1";
            }
            catch { return false; }
        }

        public void SetExtendedLoggingSetting(bool enabled)
        {
            try
            {
                Registry.SetValue(@"HKEY_CURRENT_USER\SOFTWARE\LAB02Research\HASSAgent",
                    "ExtendedLogging", enabled ? "1" : "0", RegistryValueKind.String);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SETTINGS] Error storing extended logging setting");
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void StoreInitialDefaults()
        {
            _state.AppSettings = new AppSettings();
            _state.QuickActions = new List<QuickAction>();
            _state.Commands = new List<AbstractCommand>();
            _state.SingleValueSensors = new List<AbstractSingleValueSensor>();
            _state.MultiValueSensors = new List<AbstractMultiValueSensor>();
            StoreAppSettings();
        }

        private bool LoadAppSettings()
        {
            try
            {
                if (!File.Exists(_state.AppSettingsFile))
                {
                    StoreInitialDefaults();
                    return true;
                }

                var raw = File.ReadAllText(_state.AppSettingsFile);
                if (string.IsNullOrWhiteSpace(raw)) return true;

                var settings = JsonConvert.DeserializeObject<AppSettings>(raw);
                if (settings == null)
                {
                    Log.Error("[SETTINGS] Deserialized app settings is null");
                    return false;
                }

                _state.AppSettings = settings;

                // backward-compat: treat NeverDone on existing installs as Completed
                if (_state.AppSettings.OnboardingStatus == OnboardingStatus.NeverDone)
                {
                    _state.AppSettings.OnboardingStatus = OnboardingStatus.Completed;
                    StoreAppSettings();
                }

                AgentSharedBase.SetDeviceName(_state.AppSettings.DeviceName);
                AgentSharedBase.SetCustomExecutorBinary(_state.AppSettings.CustomExecutorBinary);

                Log.Information("[SETTINGS] App settings loaded");
                return true;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "[SETTINGS] Error loading app settings: {err}", ex.Message);
                return false;
            }
        }

        private bool LoadQuickActions()
        {
            try
            {
                if (!File.Exists(_state.QuickActionsFile)) return true;

                var raw = File.ReadAllText(_state.QuickActionsFile);
                if (string.IsNullOrWhiteSpace(raw)) return true;

                var actions = JsonConvert.DeserializeObject<List<QuickAction>>(raw);
                _state.QuickActions = actions ?? new List<QuickAction>();
                Log.Information("[SETTINGS] Loaded {count} quick actions", _state.QuickActions.Count);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SETTINGS] Error loading quick actions: {err}", ex.Message);
                return false;
            }
        }

        private bool LoadCommands()
        {
            try
            {
                if (!File.Exists(_state.CommandsFile)) return true;

                var raw = File.ReadAllText(_state.CommandsFile);
                if (string.IsNullOrWhiteSpace(raw)) return true;

                var jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
                var commands = JsonConvert.DeserializeObject<List<AbstractCommand>>(raw, jsonSettings);
                _state.Commands = commands ?? new List<AbstractCommand>();
                Log.Information("[SETTINGS] Loaded {count} commands", _state.Commands.Count);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SETTINGS] Error loading commands: {err}", ex.Message);
                return false;
            }
        }

        private bool LoadSensors()
        {
            try
            {
                if (!File.Exists(_state.SensorsFile)) return true;

                var raw = File.ReadAllText(_state.SensorsFile);
                if (string.IsNullOrWhiteSpace(raw)) return true;

                var jsonSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
                var wrapper = JsonConvert.DeserializeObject<dynamic>(raw);

                if (wrapper?.SingleValue != null)
                {
                    var single = JsonConvert.DeserializeObject<List<AbstractSingleValueSensor>>(
                        wrapper.SingleValue.ToString(), jsonSettings);
                    _state.SingleValueSensors = single ?? new List<AbstractSingleValueSensor>();
                }

                if (wrapper?.MultiValue != null)
                {
                    var multi = JsonConvert.DeserializeObject<List<AbstractMultiValueSensor>>(
                        wrapper.MultiValue.ToString(), jsonSettings);
                    _state.MultiValueSensors = multi ?? new List<AbstractMultiValueSensor>();
                }

                Log.Information("[SETTINGS] Loaded {sv} single + {mv} multi-value sensors",
                    _state.SingleValueSensors.Count, _state.MultiValueSensors.Count);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[SETTINGS] Error loading sensors: {err}", ex.Message);
                return false;
            }
        }
    }
}
