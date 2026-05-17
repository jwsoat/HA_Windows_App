namespace HASS.Agent.Core.Entities;

/// <summary>
/// Catalog of all sensor types the new app can add via the UI.
/// Only includes types that ship in the HASS.Agent.Shared NuGet package (no legacy-only types).
/// </summary>
public static class SensorTypeRegistry
{
    // ── Field shortcuts ───────────────────────────────────────────────────────

    private static readonly EntityField[] WindowNameField = {
        new() { Key = "windowName", Label = "Window title", Placeholder = "Notepad" }
    };
    private static readonly EntityField[] ScreenIndexField = {
        new() { Key = "screenIndex", Label = "Screen index (0 = primary)",
                Placeholder = "0", Kind = EntityFieldKind.Number, Required = false, DefaultValue = "0" }
    };
    private static readonly EntityField[] ProcessNameField = {
        new() { Key = "processName", Label = "Process name", Placeholder = "chrome" }
    };
    private static readonly EntityField[] ServiceNameField = {
        new() { Key = "serviceName", Label = "Service name", Placeholder = "Spooler" }
    };
    private static readonly EntityField[] NetworkCardField = {
        new() { Key = "networkCard", Label = "Network card (leave blank for default)",
                Placeholder = "Ethernet", Required = false }
    };
    private static readonly EntityField[] PowershellField = {
        new() { Key = "command", Label = "PowerShell command or .ps1 path",
                Placeholder = "Get-Date", Kind = EntityFieldKind.MultilineText }
    };
    private static readonly EntityField[] WmiQueryFields = {
        new() { Key = "query", Label = "WMI query",
                Placeholder = "SELECT * FROM Win32_OperatingSystem", Kind = EntityFieldKind.MultilineText },
        new() { Key = "scope", Label = "WMI scope (leave blank for root\\cimv2)",
                Placeholder = "root\\cimv2", Required = false }
    };
    private static readonly EntityField[] PerfCounterFields = {
        new() { Key = "categoryName", Label = "Category",  Placeholder = "Processor" },
        new() { Key = "counterName",  Label = "Counter",   Placeholder = "% Processor Time" },
        new() { Key = "instanceName", Label = "Instance (leave blank if none)",
                Placeholder = "_Total", Required = false }
    };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private const string SingleValueNs = "HASS.Agent.Shared.HomeAssistant.Sensors.GeneralSensors.SingleValue.";
    private const string MultiValueNs  = "HASS.Agent.Shared.HomeAssistant.Sensors.GeneralSensors.MultiValue.";
    private const string PerfCounterNs = "HASS.Agent.Shared.HomeAssistant.Sensors.PerfCounterSensors.SingleValue.";
    private const string WmiSensorNs   = "HASS.Agent.Shared.HomeAssistant.Sensors.WmiSensors.SingleValue.";
    private const string TopLevelNs    = "HASS.Agent.Shared.HomeAssistant.Sensors.";

    // ── Catalog ───────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<EntityTypeMetadata> All = new EntityTypeMetadata[]
    {
        // System
        Single("ActiveWindowSensor",         "Active Window",          "System", "Title of the currently focused window.", 30, ns: SingleValueNs),
        Single("LastActiveSensor",           "Last Active",            "System", "Seconds since last user input.",            10, ns: SingleValueNs),
        Single("LastBootSensor",             "Last Boot",              "System", "Timestamp of the last boot.",               300, ns: SingleValueNs),
        Single("LastSystemStateChangeSensor","Last System State Change","System","Last lock/unlock/login/logout/sleep event.",  5, ns: SingleValueNs),
        Single("LoggedUserSensor",           "Logged-in User",         "System", "Username currently logged in.",              30, ns: SingleValueNs),
        Single("LoggedUsersSensor",          "Logged-in Users",        "System", "All users currently logged in.",             60, ns: SingleValueNs),
        Single("SessionStateSensor",         "Session State",          "System", "Locked / unlocked / disconnected etc.",      10, ns: SingleValueNs),
        Single("UserNotificationStateSensor","Notification Mode",      "System", "Focus assist / quiet hours / presenting…",   10, ns: SingleValueNs),
        Single("DummySensor",                "Dummy",                  "System", "Always returns 'ok' — useful for liveness.", 60, ns: SingleValueNs),

        // Hardware / performance
        Single("CpuLoadSensor",              "CPU Load",               "Hardware", "Overall CPU usage %.",                     5,  ns: PerfCounterNs),
        Single("MemoryUsageSensor",          "Memory Usage",           "Hardware", "RAM usage %.",                              5,  ns: WmiSensorNs),
        Single("CurrentClockSpeedSensor",    "CPU Clock Speed",        "Hardware", "Current CPU clock in MHz.",                 10, ns: WmiSensorNs),
        Single("GpuLoadSensor",              "GPU Load",               "Hardware", "GPU usage % (requires LibreHardwareMonitor).", 5,  ns: SingleValueNs),
        Single("GpuTemperatureSensor",       "GPU Temperature",        "Hardware", "GPU °C (requires LibreHardwareMonitor).",   10, ns: SingleValueNs),

        // Media / IO
        Single("CurrentVolumeSensor",        "Current Volume",         "Media",    "Master volume %.",                          5,  ns: SingleValueNs),
        Single("MicrophoneActiveSensor",     "Microphone Active",      "Media",    "Whether any process is using the mic.",     5,  ns: SingleValueNs),
        Single("MicrophoneProcessSensor",    "Microphone Process",     "Media",    "Which process is using the mic.",           5,  ns: SingleValueNs),
        Single("WebcamActiveSensor",         "Webcam Active",          "Media",    "Whether any process is using the webcam.",  5,  ns: SingleValueNs),
        Single("WebcamProcessSensor",        "Webcam Process",         "Media",    "Which process is using the webcam.",        5,  ns: SingleValueNs),

        // Multi-value
        Multi ("AudioSensors",               "Audio Devices",          "Media",    "Per-device audio info (volume, default, etc.)", 10),
        Multi ("BatterySensors",             "Battery",                "Hardware", "Charge level, state, time remaining.",      30),
        Multi ("DisplaySensors",             "Displays",               "Hardware", "Connected displays and their state.",        30),
        Multi ("NetworkSensors",             "Network",                "Hardware", "Network adapter throughput & status.",       10, NetworkCardField),
        Multi ("StorageSensors",             "Storage",                "Hardware", "Disk usage, free space.",                    60),
        Multi ("WindowsUpdatesSensors",      "Windows Updates",        "System",   "Pending Windows Updates.",                  3600),

        // Parameterized
        Single("NamedWindowSensor",          "Named Window",           "Custom", "Whether a window with the given title is open.", 5, WindowNameField, ns: SingleValueNs),
        Single("NamedActiveWindowSensor",    "Named Active Window",    "Custom", "Whether the focused window matches a given title.", 5, WindowNameField, ns: SingleValueNs),
        Single("ScreenshotSensor",           "Screenshot",             "Custom", "Periodically captures and uploads a screenshot.", 15, ScreenIndexField, ns: SingleValueNs),
        Single("ProcessActiveSensor",        "Process Active",         "Custom", "Whether a named process is running.",       5,  ProcessNameField, ns: SingleValueNs),
        Single("WindowStateSensor",          "Window State",           "Custom", "Minimized / maximized / hidden for a process.", 5, ProcessNameField, ns: SingleValueNs),
        Single("ServiceStateSensor",         "Service State",          "Custom", "Running / stopped state of a Windows service.", 10, ServiceNameField, ns: SingleValueNs),
        Single("PowershellSensor",           "PowerShell",             "Custom", "Returns the output of a PowerShell command/script.", 30, PowershellField, ns: TopLevelNs),
        Single("WmiQuerySensor",             "WMI Query",              "Custom", "Returns the result of a WMI query.",        30, WmiQueryFields, ns: TopLevelNs),
        Single("PerformanceCounterSensor",   "Performance Counter",    "Custom", "Reads a Windows performance counter.",       5, PerfCounterFields, ns: TopLevelNs),
    };

    public static EntityTypeMetadata? Find(string key) =>
        All.FirstOrDefault(m => m.Key == key);

    // ── Builders ──────────────────────────────────────────────────────────────

    private static EntityTypeMetadata Single(
        string key, string display, string category, string description,
        int interval, EntityField[]? fields = null, string ns = SingleValueNs)
        => new()
        {
            Key = key, DisplayName = display, Category = category, Description = description,
            FullTypeName = ns + key,
            IsMultiValue = false,
            DefaultIntervalSeconds = interval,
            Fields = fields ?? Array.Empty<EntityField>()
        };

    private static EntityTypeMetadata Multi(
        string key, string display, string category, string description,
        int interval, EntityField[]? fields = null)
        => new()
        {
            Key = key, DisplayName = display, Category = category, Description = description,
            FullTypeName = MultiValueNs + key,
            IsMultiValue = true,
            DefaultIntervalSeconds = interval,
            Fields = fields ?? Array.Empty<EntityField>()
        };
}
