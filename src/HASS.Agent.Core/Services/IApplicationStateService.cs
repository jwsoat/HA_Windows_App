using HASS.Agent.Core.Models.Config;
using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Shared.Models.HomeAssistant;

namespace HASS.Agent.Core.Services
{
    /// <summary>
    /// Central runtime state — replaces the Variables god-object singleton.
    /// All mutable state that multiple services need is here and observable.
    /// </summary>
    public interface IApplicationStateService
    {
        bool ShuttingDown { get; set; }
        bool ExtendedLogging { get; set; }
        bool ChildApplicationMode { get; set; }

        AppSettings AppSettings { get; set; }
        DeviceConfigModel? DeviceConfig { get; set; }

        List<QuickAction> QuickActions { get; set; }
        List<AbstractCommand> Commands { get; set; }
        List<AbstractSingleValueSensor> SingleValueSensors { get; set; }
        List<AbstractMultiValueSensor> MultiValueSensors { get; set; }

        string StartupPath { get; }
        string ConfigPath { get; }
        string AppSettingsFile { get; }
        string QuickActionsFile { get; }
        string CommandsFile { get; }
        string SensorsFile { get; }
        string CachePath { get; }
        string ImageCachePath { get; }
        string AudioCachePath { get; }
        string LogPath { get; }

        string SerialNumber { get; }
    }
}
