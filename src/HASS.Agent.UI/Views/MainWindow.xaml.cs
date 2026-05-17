using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using HASS.Agent.Core.Hotkeys;
using HASS.Agent.Core.Services;
using HASS.Agent.UI.Services;
using HASS.Agent.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace HASS.Agent.UI.Views;

/// <summary>
/// Invisible 1×1 host window. Provides the HWND sink for Win32 hotkey and tray icon messages.
/// All visible UI is opened as separate windows from here.
/// </summary>
public sealed partial class MainWindow : Window
{
    public nint Hwnd { get; private set; }

    // ── WndProc subclassing ───────────────────────────────────────────────────

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nint WndProcDelegate(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nint wParam, nint lParam);

    private const int GWLP_WNDPROC = -4;
    private const uint WM_HOTKEY = 0x0312;
    private const uint WM_DESTROY = 0x0002;

    private WndProcDelegate? _newWndProc;
    private nint _oldWndProc;

    // ── Services ──────────────────────────────────────────────────────────────

    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly TrayIconService _trayService;
    private readonly IApplicationStateService _state;
    private readonly QuickActionsOverlayViewModel _qaViewModel;

    public MainWindow(
        IGlobalHotkeyService hotkeyService,
        TrayIconService trayService,
        IApplicationStateService state,
        QuickActionsOverlayViewModel qaViewModel)
    {
        _hotkeyService = hotkeyService;
        _trayService = trayService;
        _state = state;
        _qaViewModel = qaViewModel;

        InitializeComponent();

        Hwnd = WindowNative.GetWindowHandle(this);

        HideFromTaskbar();
        SubclassWndProc();

        // Defer hotkey/tray init until settings are loaded
        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (_, _) => OnSettingsLoaded());

        // Show quick actions overlay when hotkey fires
        WeakReferenceMessenger.Default.Register<QuickActionsHotkeyMessage>(this, (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var overlay = new QuickActionsOverlay(_qaViewModel);
                overlay.ShowOverlay();
            });
        });

        // Open settings shell from tray or hotkey
        WeakReferenceMessenger.Default.Register<OpenSettingsMessage>(this, (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                var shell = App.Services.GetRequiredService<ShellWindow>();
                shell.Activate();
            });
        });

        // Exit app from tray. Application.Exit() terminates the whole process
        // (closes any open Shell/Overlay windows along with MainWindow).
        WeakReferenceMessenger.Default.Register<ExitAppMessage>(this, (_, _) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _state.ShuttingDown = true;
                _trayService.RemoveTrayIcon();
                _hotkeyService.UnregisterAll(Hwnd);
                Application.Current.Exit();
            });
        });

        this.Closed += OnClosed;
    }

    private void HideFromTaskbar()
    {
        var windowId = Win32Interop.GetWindowIdFromWindow(Hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.IsShownInSwitchers = false;
        appWindow.Resize(new Windows.Graphics.SizeInt32(1, 1));
        appWindow.Move(new Windows.Graphics.PointInt32(-32000, -32000));
    }

    private void SubclassWndProc()
    {
        _newWndProc = WndProc;
        var ptr = Marshal.GetFunctionPointerForDelegate(_newWndProc);
        _oldWndProc = SetWindowLongPtr(Hwnd, GWLP_WNDPROC, ptr);
    }

    private nint WndProc(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case WM_HOTKEY:
                _hotkeyService.HandleHotkeyMessage(wParam);
                return 0;

            case TrayIconService.WM_TRAYICON:
                _trayService.HandleTrayMessage(lParam);
                return 0;

            case WM_DESTROY:
                _hotkeyService.UnregisterAll(hWnd);
                _trayService.RemoveTrayIcon();
                break;
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void OnSettingsLoaded()
    {
        // Register hotkeys now that AppSettings is populated
        _hotkeyService.RegisterQuickActionsHotkey(Hwnd);
        _hotkeyService.RegisterQuickActionHotkeys(Hwnd);

        // Add tray icon
        _trayService.Initialize(Hwnd);
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _state.ShuttingDown = true;
        _hotkeyService.UnregisterAll(Hwnd);
        _trayService.RemoveTrayIcon();
    }
}
