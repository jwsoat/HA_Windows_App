using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Core.Services;
using Serilog;

namespace HASS.Agent.Core.Hotkeys
{
    /// <summary>
    /// Global hotkey registration via Win32 RegisterHotKey — no WinForms dependency.
    /// The host window's HWND must be passed in; WM_HOTKEY messages are processed via HandleHotkeyMessage.
    /// </summary>
    public class GlobalHotkeyService : IGlobalHotkeyService
    {
        private readonly IApplicationStateService _state;

        private const int WM_HOTKEY = 0x0312;
        private const int QuickActionsHotkeyId = 9000;
        private const int QuickActionBaseId = 9001; // IDs 9001..9999 for per-action hotkeys

        private readonly Dictionary<int, string> _hotkeyIdToActionKey = new();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(nint hWnd, int id);

        // Modifier flags
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        public GlobalHotkeyService(IApplicationStateService state)
        {
            _state = state;
        }

        public bool RegisterQuickActionsHotkey(nint hwnd)
        {
            var settings = _state.AppSettings;
            if (!settings.QuickActionsHotKeyEnabled) return false;

            var hotkey = string.IsNullOrWhiteSpace(settings.QuickActionsHotKey)
                ? "Ctrl+Alt+Q"
                : settings.QuickActionsHotKey;

            if (!TryParseHotkey(hotkey, out var modifiers, out var vk))
            {
                Log.Warning("[HOTKEY] Could not parse quick actions hotkey: {hk}", hotkey);
                return false;
            }

            UnregisterHotKey(hwnd, QuickActionsHotkeyId);

            if (!RegisterHotKey(hwnd, QuickActionsHotkeyId, modifiers | MOD_NOREPEAT, vk))
            {
                Log.Error("[HOTKEY] Failed to register quick actions hotkey {hk} (error {err})", hotkey, Marshal.GetLastWin32Error());
                return false;
            }

            Log.Information("[HOTKEY] Registered quick actions hotkey: {hk}", hotkey);
            return true;
        }

        public void RegisterQuickActionHotkeys(nint hwnd)
        {
            _hotkeyIdToActionKey.Clear();
            var id = QuickActionBaseId;

            foreach (var action in _state.QuickActions.Where(a => a.HotKeyEnabled && !string.IsNullOrWhiteSpace(a.HotKey)))
            {
                if (TryParseHotkey(action.HotKey, out var mods, out var vk))
                {
                    if (RegisterHotKey(hwnd, id, mods | MOD_NOREPEAT, vk))
                    {
                        _hotkeyIdToActionKey[id] = action.HotKey;
                        Log.Debug("[HOTKEY] Registered action hotkey {hk} (id {id})", action.HotKey, id);
                        id++;
                    }
                }
            }
        }

        public void UnregisterAll(nint hwnd)
        {
            UnregisterHotKey(hwnd, QuickActionsHotkeyId);
            foreach (var id in _hotkeyIdToActionKey.Keys)
                UnregisterHotKey(hwnd, id);
            _hotkeyIdToActionKey.Clear();
        }

        public bool HandleHotkeyMessage(nint wParam)
        {
            var id = (int)wParam;

            if (id == QuickActionsHotkeyId)
            {
                WeakReferenceMessenger.Default.Send(new QuickActionsHotkeyMessage());
                return true;
            }

            if (_hotkeyIdToActionKey.TryGetValue(id, out var hotkey))
            {
                WeakReferenceMessenger.Default.Send(new QuickActionHotkeyMessage(hotkey));
                return true;
            }

            return false;
        }

        // ── Hotkey string parsing ─────────────────────────────────────────────
        // Parses strings like "Ctrl+Alt+Q", "Shift+F1", "Win+Ctrl+A"

        private static bool TryParseHotkey(string hotkey, out uint modifiers, out uint vk)
        {
            modifiers = 0;
            vk = 0;

            if (string.IsNullOrWhiteSpace(hotkey)) return false;

            var parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries);
            string? keyPart = null;

            foreach (var part in parts)
            {
                var p = part.Trim().ToLowerInvariant();
                switch (p)
                {
                    case "ctrl":
                    case "control":
                        modifiers |= MOD_CONTROL;
                        break;
                    case "alt":
                        modifiers |= MOD_ALT;
                        break;
                    case "shift":
                        modifiers |= MOD_SHIFT;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= MOD_WIN;
                        break;
                    default:
                        keyPart = part.Trim();
                        break;
                }
            }

            if (keyPart == null) return false;

            vk = KeyNameToVk(keyPart);
            return vk != 0;
        }

        private static uint KeyNameToVk(string key)
        {
            // Single letter A-Z
            if (key.Length == 1 && char.IsLetter(key[0]))
                return (uint)char.ToUpper(key[0]);

            // Single digit 0-9
            if (key.Length == 1 && char.IsDigit(key[0]))
                return (uint)key[0];

            return key.ToUpperInvariant() switch
            {
                "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
                "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
                "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
                "HOME" => 0x24, "END" => 0x23,
                "PAGEUP" or "PGUP" => 0x21, "PAGEDOWN" or "PGDN" => 0x22,
                "INSERT" or "INS" => 0x2D, "DELETE" or "DEL" => 0x2E,
                "UP" => 0x26, "DOWN" => 0x28, "LEFT" => 0x25, "RIGHT" => 0x27,
                "SPACE" => 0x20, "ENTER" or "RETURN" => 0x0D, "ESCAPE" or "ESC" => 0x1B,
                "TAB" => 0x09, "BACKSPACE" or "BACK" => 0x08,
                "PAUSE" => 0x13, "CAPSLOCK" => 0x14, "NUMLOCK" => 0x90,
                "SCROLLLOCK" => 0x91, "PRINTSCREEN" or "PRTSCN" => 0x2C,
                _ => 0
            };
        }
    }
}
