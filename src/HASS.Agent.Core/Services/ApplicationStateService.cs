using HASS.Agent.Core.Models.Config;
using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Shared.Models.HomeAssistant;
using Microsoft.Win32;

namespace HASS.Agent.Core.Services
{
    public class ApplicationStateService : IApplicationStateService
    {
        public ApplicationStateService()
        {
            var exe = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? string.Empty;
            StartupPath = System.IO.Path.GetDirectoryName(exe) ?? AppContext.BaseDirectory;

            ConfigPath = System.IO.Path.Combine(StartupPath, "config");
            AppSettingsFile = System.IO.Path.Combine(ConfigPath, "appsettings.json");
            QuickActionsFile = System.IO.Path.Combine(ConfigPath, "quickactions.json");
            CommandsFile = System.IO.Path.Combine(ConfigPath, "commands.json");
            SensorsFile = System.IO.Path.Combine(ConfigPath, "sensors.json");
            CachePath = System.IO.Path.Combine(StartupPath, "cache");
            ImageCachePath = System.IO.Path.Combine(CachePath, "images");
            AudioCachePath = System.IO.Path.Combine(CachePath, "audio");
            LogPath = System.IO.Path.Combine(StartupPath, "logs");

            SerialNumber = LoadOrCreateSerialNumber();
        }

        public bool ShuttingDown { get; set; }
        public bool ExtendedLogging { get; set; }
        public bool ChildApplicationMode { get; set; }

        public AppSettings AppSettings { get; set; } = new();
        public DeviceConfigModel? DeviceConfig { get; set; }

        public List<QuickAction> QuickActions { get; set; } = new();
        public List<AbstractCommand> Commands { get; set; } = new();
        public List<AbstractSingleValueSensor> SingleValueSensors { get; set; } = new();
        public List<AbstractMultiValueSensor> MultiValueSensors { get; set; } = new();

        public string StartupPath { get; }
        public string ConfigPath { get; }
        public string AppSettingsFile { get; }
        public string QuickActionsFile { get; }
        public string CommandsFile { get; }
        public string SensorsFile { get; }
        public string CachePath { get; }
        public string ImageCachePath { get; }
        public string AudioCachePath { get; }
        public string LogPath { get; }
        public string SerialNumber { get; }

        private static string LoadOrCreateSerialNumber()
        {
            const string regKey = @"HKEY_CURRENT_USER\SOFTWARE\LAB02Research\HASSAgent";
            const string regVal = "DeviceSerial";

            var existing = Registry.GetValue(regKey, regVal, null) as string;
            if (!string.IsNullOrWhiteSpace(existing))
                return existing;

            var serial = Guid.NewGuid().ToString("N").ToUpper();
            Registry.SetValue(regKey, regVal, serial);
            return serial;
        }
    }
}
