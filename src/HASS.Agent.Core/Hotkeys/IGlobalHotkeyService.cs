namespace HASS.Agent.Core.Hotkeys
{
    public interface IGlobalHotkeyService
    {
        /// <summary>Registers the global Quick Actions hotkey from AppSettings.</summary>
        bool RegisterQuickActionsHotkey(nint hwnd);

        /// <summary>Registers per-action hotkeys from the QuickActions list.</summary>
        void RegisterQuickActionHotkeys(nint hwnd);

        /// <summary>Unregisters all hotkeys.</summary>
        void UnregisterAll(nint hwnd);

        /// <summary>
        /// Call this from the host window's WndProc when WM_HOTKEY (0x0312) is received.
        /// Returns true if the message was consumed.
        /// </summary>
        bool HandleHotkeyMessage(nint wParam);
    }
}
