using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using HASS.Agent.Core.Services;
using Serilog;

namespace HASS.Agent.UI.Services;

/// <summary>
/// System tray icon via Shell_NotifyIcon Win32 API. No WinForms dependency.
/// </summary>
public class TrayIconService : IDisposable
{
    // ── Win32 P/Invoke ────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public nint hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA pnid);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint LoadImage(nint hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(nint hMenu, uint uFlags, nuint uIDNewItem, string? lpNewItem);

    [DllImport("user32.dll")]
    private static extern bool TrackPopupMenu(nint hMenu, uint uFlags, int x, int y, int nReserved, nint hWnd, nint prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(nint hMenu);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x; public int y; }

    // ── Constants ─────────────────────────────────────────────────────────────

    private const uint NIM_ADD = 0;
    private const uint NIM_MODIFY = 1;
    private const uint NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 0x01;
    private const uint NIF_ICON = 0x02;
    private const uint NIF_TIP = 0x04;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;
    private const uint LR_DEFAULTSIZE = 0x0040;

    public const uint WM_TRAYICON = 0x0401; // WM_USER + 1
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;

    private const uint MF_STRING = 0x00;
    private const uint MF_SEPARATOR = 0x800;
    private const uint MF_GRAYED = 0x01;
    private const uint TPM_RIGHTBUTTON = 0x02;
    private const uint TPM_BOTTOMALIGN = 0x20;

    // Context menu command IDs
    private const nuint CMD_OPEN_SETTINGS = 1;
    private const nuint CMD_QUICK_ACTIONS = 2;
    private const nuint CMD_RELOAD = 3;
    private const nuint CMD_EXIT = 9;

    // ── State ─────────────────────────────────────────────────────────────────

    private nint _hwnd;
    private nint _hIcon;
    private bool _initialized;
    private readonly IApplicationStateService _state;

    public TrayIconService(IApplicationStateService state)
    {
        _state = state;
    }

    public void Initialize(nint hwnd)
    {
        _hwnd = hwnd;

        // Load icon from the executable's directory
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "hassagent.ico");
        if (File.Exists(iconPath))
            _hIcon = LoadImage(nint.Zero, iconPath, IMAGE_ICON, 0, 0, LR_LOADFROMFILE | LR_DEFAULTSIZE);

        var nid = BuildNid();
        nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        nid.szTip = "HASS.Agent";

        Shell_NotifyIcon(NIM_ADD, ref nid);
        _initialized = true;

        Log.Information("[TRAY] Tray icon initialized");
    }

    public void RemoveTrayIcon()
    {
        if (!_initialized) return;
        var nid = BuildNid();
        Shell_NotifyIcon(NIM_DELETE, ref nid);
        if (_hIcon != nint.Zero) DestroyIcon(_hIcon);
        _initialized = false;
    }

    public void HandleTrayMessage(nint lParam)
    {
        var notifyMsg = (uint)(lParam & 0xFFFF);

        switch (notifyMsg)
        {
            case WM_LBUTTONDBLCLK:
                OpenSettings();
                break;

            case WM_RBUTTONUP:
                ShowContextMenu();
                break;
        }
    }

    private void ShowContextMenu()
    {
        GetCursorPos(out var pos);
        SetForegroundWindow(_hwnd);

        var menu = CreatePopupMenu();
        try
        {
            AppendMenu(menu, MF_STRING, CMD_OPEN_SETTINGS, "Open Settings");
            AppendMenu(menu, MF_STRING, CMD_QUICK_ACTIONS, "Quick Actions");
            AppendMenu(menu, MF_SEPARATOR, 0, null);
            AppendMenu(menu, MF_STRING, CMD_RELOAD, "Reload Configuration");
            AppendMenu(menu, MF_SEPARATOR, 0, null);
            AppendMenu(menu, MF_STRING, CMD_EXIT, "Exit");

            TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, pos.x, pos.y, 0, _hwnd, nint.Zero);
        }
        finally
        {
            DestroyMenu(menu);
        }
    }

    private static void OpenSettings()
    {
        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage());
    }

    private NOTIFYICONDATA BuildNid() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _hwnd,
        uID = 1,
        uCallbackMessage = WM_TRAYICON,
        hIcon = _hIcon,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty
    };

    public void Dispose()
    {
        RemoveTrayIcon();
        GC.SuppressFinalize(this);
    }
}

// Message sent when the user opens settings from the tray
public sealed class OpenSettingsMessage { }
