using Microsoft.Win32;
using Serilog;

namespace HASS.Agent.Core.Configuration;

/// <summary>
/// First-launch migration from the LAB02 WinForms version (which stored config next to its
/// executable, typically C:\Program Files\HASS.Agent\config). If our config directory is empty
/// and a legacy install is found, copy its JSON config files in so the user doesn't lose data.
/// Backs up the legacy config dir first (per hass-agent/HASS.Agent PR #377).
/// </summary>
public static class LegacyConfigMigrator
{
    private static readonly string[] CandidateInstallDirs =
    {
        @"C:\Program Files\HASS.Agent",
        @"C:\Program Files (x86)\HASS.Agent",
        // Legacy LAB02 installer used a different name pattern
        @"C:\Program Files\LAB02 Research\HASS.Agent",
        @"C:\Program Files (x86)\LAB02 Research\HASS.Agent",
    };

    private static readonly string[] ConfigFiles =
    {
        "appsettings.json",
        "quickactions.json",
        "commands.json",
        "sensors.json"
    };

    /// <summary>
    /// If <paramref name="targetConfigDir"/> has no appsettings.json yet and a legacy install is
    /// found, copy the legacy config files in. Returns true if a migration ran.
    /// </summary>
    public static bool MigrateIfNeeded(string targetConfigDir)
    {
        try
        {
            var ourSettings = Path.Combine(targetConfigDir, "appsettings.json");
            if (File.Exists(ourSettings)) return false; // already have config

            var source = FindLegacyConfigDir();
            if (source == null)
            {
                Log.Information("[MIGRATE] No legacy install found — fresh start");
                return false;
            }

            Log.Information("[MIGRATE] Legacy config found at {src} — copying into {dst}", source, targetConfigDir);
            Directory.CreateDirectory(targetConfigDir);

            // Backup the legacy folder before we touch anything (the user keeps the legacy app
            // working and can roll back if needed).
            BackupLegacyDir(source);

            var copied = 0;
            foreach (var f in ConfigFiles)
            {
                var src = Path.Combine(source, f);
                var dst = Path.Combine(targetConfigDir, f);
                if (!File.Exists(src)) continue;
                File.Copy(src, dst, overwrite: true);
                copied++;
            }

            Log.Information("[MIGRATE] Copied {n} config files", copied);
            return copied > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[MIGRATE] Legacy config migration failed: {err}", ex.Message);
            return false;
        }
    }

    private static string? FindLegacyConfigDir()
    {
        // Try the standard install paths first.
        foreach (var dir in CandidateInstallDirs)
        {
            var cfg = Path.Combine(dir, "config");
            if (Directory.Exists(cfg) && File.Exists(Path.Combine(cfg, "appsettings.json")))
                return cfg;
        }

        // Last resort — ask the Windows uninstaller registry where HASS.Agent is installed.
        try
        {
            foreach (var root in new[]
            {
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
                Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            })
            {
                if (root == null) continue;
                foreach (var keyName in root.GetSubKeyNames())
                {
                    using var sub = root.OpenSubKey(keyName);
                    var display = sub?.GetValue("DisplayName") as string;
                    if (display == null || !display.StartsWith("HASS.Agent", StringComparison.OrdinalIgnoreCase)) continue;

                    var loc = sub?.GetValue("InstallLocation") as string;
                    if (string.IsNullOrWhiteSpace(loc)) continue;

                    var cfg = Path.Combine(loc, "config");
                    if (Directory.Exists(cfg) && File.Exists(Path.Combine(cfg, "appsettings.json")))
                        return cfg;
                }
            }
        }
        catch { /* registry probing is best-effort */ }

        return null;
    }

    private static void BackupLegacyDir(string source)
    {
        try
        {
            var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var backup = Path.Combine(Path.GetDirectoryName(source)!, $"config.backup-{ts}");
            Directory.CreateDirectory(backup);
            foreach (var f in Directory.GetFiles(source))
                File.Copy(f, Path.Combine(backup, Path.GetFileName(f)), overwrite: false);
            Log.Information("[MIGRATE] Legacy config backed up to {backup}", backup);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[MIGRATE] Could not back up legacy config (continuing): {err}", ex.Message);
        }
    }
}
