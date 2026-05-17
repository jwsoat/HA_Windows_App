namespace HASS.Agent.Core.Entities;

/// <summary>
/// Catalog of all command types the new app can add via the UI.
/// </summary>
public static class CommandTypeRegistry
{
    // ── Field shortcuts ───────────────────────────────────────────────────────

    private static readonly EntityField[] CustomFields = {
        new() { Key = "command", Label = "Command line",
                Placeholder = "cmd /c echo hello", Kind = EntityFieldKind.MultilineText },
        new() { Key = "runAsLowIntegrity", Label = "Run as low-integrity process",
                Kind = EntityFieldKind.Boolean, Required = false, DefaultValue = "false" }
    };
    private static readonly EntityField[] PowershellFields = {
        new() { Key = "command", Label = "PowerShell command or .ps1 path",
                Placeholder = "Get-Date", Kind = EntityFieldKind.MultilineText }
    };
    private static readonly EntityField[] CustomExecutorFields = {
        new() { Key = "command", Label = "Argument to pass to custom executor",
                Placeholder = "task --do-thing", Kind = EntityFieldKind.MultilineText }
    };
    private static readonly EntityField[] KeyCodeField = {
        new() { Key = "keyCode", Label = "Virtual key code (0–255)",
                Placeholder = "65 = 'A'", Kind = EntityFieldKind.Number }
    };
    private static readonly EntityField[] MultipleKeysField = {
        new() { Key = "keys", Label = "Comma-separated virtual key codes",
                Placeholder = "162, 65    (LCtrl + A)", Kind = EntityFieldKind.KeyCodeList }
    };
    private static readonly EntityField[] SendWindowField = {
        new() { Key = "process", Label = "Process name",
                Placeholder = "notepad" }
    };
    private static readonly EntityField[] SetVolumeField = {
        new() { Key = "volume", Label = "Default volume on press (0–100)",
                Placeholder = "50", Kind = EntityFieldKind.Number, Required = false, DefaultValue = "50" }
    };
    private static readonly EntityField[] AppVolumeFields = {
        new() { Key = "commandConfig", Label = "Process name + volume (e.g. \"chrome:50\")",
                Placeholder = "chrome:50" }
    };
    private static readonly EntityField[] AudioDeviceField = {
        new() { Key = "audioDevice", Label = "Audio device name (must match Windows exactly)",
                Placeholder = "Speakers (Realtek)" }
    };

    // ── Namespaces ────────────────────────────────────────────────────────────

    private const string CustomNs   = "HASS.Agent.Shared.HomeAssistant.Commands.CustomCommands.";
    private const string KeyNs      = "HASS.Agent.Shared.HomeAssistant.Commands.KeyCommands.";
    private const string InternalNs = "HASS.Agent.Shared.HomeAssistant.Commands.InternalCommands.";
    private const string TopNs      = "HASS.Agent.Shared.HomeAssistant.Commands.";

    // ── Catalog ───────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<EntityTypeMetadata> All = new EntityTypeMetadata[]
    {
        // System power
        Cmd("ShutdownCommand",    "Shutdown",     "System", "Shut the machine down.",            CustomNs),
        Cmd("RestartCommand",     "Restart",      "System", "Restart the machine.",              CustomNs),
        Cmd("HibernateCommand",   "Hibernate",    "System", "Hibernate the machine.",            CustomNs),
        Cmd("SleepCommand",       "Sleep",        "System", "Put the machine to sleep.",         CustomNs),
        Cmd("LockCommand",        "Lock",         "System", "Lock the current session.",         CustomNs),
        Cmd("LogOffCommand",      "Log Off",      "System", "Log the current user off.",         CustomNs),

        // Monitor
        Cmd("MonitorSleepCommand",          "Monitor Sleep",             "System", "Turn the monitor off.",                                                  InternalNs),
        Cmd("MonitorSleepPowerPlanCommand", "Monitor Sleep (Power Plan)","System", "Turn the screen off via the power plan (won't put the system to sleep).", InternalNs),
        Cmd("MonitorWakeCommand",           "Monitor Wake",              "System", "Wake the monitor.",                                                      KeyNs),

        // Media
        Cmd("MediaPlayPauseCommand",  "Media Play/Pause",    "Media", "Toggle play/pause.",      KeyNs),
        Cmd("MediaNextCommand",       "Media Next",          "Media", "Next track.",             KeyNs),
        Cmd("MediaPreviousCommand",   "Media Previous",      "Media", "Previous track.",         KeyNs),
        Cmd("MediaVolumeUpCommand",   "Media Volume Up",     "Media", "Raise system volume.",    KeyNs),
        Cmd("MediaVolumeDownCommand", "Media Volume Down",   "Media", "Lower system volume.",    KeyNs),
        Cmd("MediaMuteCommand",       "Media Mute",          "Media", "Mute system volume.",     KeyNs),
        Cmd("SetVolumeCommand",            "Set Volume",            "Media", "Set system volume to a value.",                       InternalNs, SetVolumeField),
        Cmd("SetApplicationVolumeCommand", "Set App Volume",        "Media", "Set the volume of a specific application.",          InternalNs, AppVolumeFields),
        Cmd("SetAudioOutputCommand",       "Set Audio Output",      "Media", "Set default audio output device.",                    InternalNs, AudioDeviceField),
        Cmd("SetAudioInputCommand",        "Set Audio Input",       "Media", "Set default audio input (microphone) device.",        InternalNs, AudioDeviceField),

        // Parameterized
        Cmd("CustomCommand",            "Custom Command",       "Custom",
            "Run an arbitrary command line.", TopNs, CustomFields),
        Cmd("PowershellCommand",        "PowerShell",           "Custom",
            "Run a PowerShell command or script.", TopNs, PowershellFields),
        Cmd("CustomExecutorCommand",    "Custom Executor",      "Custom",
            "Run via the configured custom executor (e.g. a launcher).", InternalNs, CustomExecutorFields),
        Cmd("KeyCommand",               "Key Press",            "Custom",
            "Simulate pressing a single virtual key.", TopNs, KeyCodeField),
        Cmd("MultipleKeysCommand",      "Multiple Keys",        "Custom",
            "Simulate pressing a sequence of virtual keys.", TopNs, MultipleKeysField),
        Cmd("SendWindowToFrontCommand", "Send Window to Front", "Custom",
            "Bring a process's main window to the foreground.", InternalNs, SendWindowField),
    };

    public static EntityTypeMetadata? Find(string key) =>
        All.FirstOrDefault(m => m.Key == key);

    // ── Builder ───────────────────────────────────────────────────────────────

    private static EntityTypeMetadata Cmd(string key, string display, string category,
        string description, string ns, EntityField[]? fields = null)
        => new()
        {
            Key = key, DisplayName = display, Category = category, Description = description,
            FullTypeName = ns + key,
            IsMultiValue = false,
            Fields = fields ?? Array.Empty<EntityField>()
        };
}
