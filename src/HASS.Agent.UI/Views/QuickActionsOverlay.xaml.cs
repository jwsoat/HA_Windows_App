using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Core.Services;
using HASS.Agent.UI.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;

namespace HASS.Agent.UI.Views;

/// <summary>
/// Acrylic-backed quick actions overlay. Opens centered on the active monitor,
/// dismisses on ESC or focus loss, supports arrow-key navigation and Enter to execute.
/// </summary>
public sealed partial class QuickActionsOverlay : Window
{
    public QuickActionsOverlayViewModel ViewModel { get; }

    // ── Win32 for popup window style ──────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    // ── Backdrop ──────────────────────────────────────────────────────────────

    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _backdropConfig;

    // ── Width / height of overlay ─────────────────────────────────────────────

    private const int OverlayWidth = 960;
    private const int OverlayHeight = 700;

    public QuickActionsOverlay(QuickActionsOverlayViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        var hwnd = WindowNative.GetWindowHandle(this);
        ConfigureWindowStyle(hwnd);
        ConfigureAppWindow();
        SetupAcrylicBackdrop();
        CenterOnActiveMonitor(hwnd);

        RootGrid.KeyDown += OnKeyDown;
        RootGrid.IsTabStop = true;
        RootGrid.Focus(FocusState.Programmatic);

        WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(
            this, (_, _) => ViewModel.Load());
    }

    public void ShowOverlay()
    {
        ViewModel.Load();
        Activate();
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void OnCardClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is QuickAction action)
            _ = Task.Run(() => ViewModel.ExecuteActionCommand.Execute(action));
        Close();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    // ── Window configuration ──────────────────────────────────────────────────

    private static void ConfigureWindowStyle(nint hwnd)
    {
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW; // No taskbar / alt-tab entry
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    private void ConfigureAppWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.IsShownInSwitchers = false;
        appWindow.Resize(new SizeInt32(OverlayWidth, OverlayHeight));

        if (appWindow.Presenter is OverlappedPresenter p)
        {
            p.IsResizable = false;
            p.IsMaximizable = false;
            p.IsMinimizable = false;
        }
    }

    private void SetupAcrylicBackdrop()
    {
        if (!DesktopAcrylicController.IsSupported()) return;

        _backdropConfig = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Dark
        };

        _acrylicController = new DesktopAcrylicController
        {
            TintColor = Windows.UI.Color.FromArgb(255, 30, 30, 30),
            TintOpacity = 0.7f,
            LuminosityOpacity = 0.0f,
            Kind = DesktopAcrylicKind.Base
        };

        _acrylicController.AddSystemBackdropTarget(
            this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
        _acrylicController.SetSystemBackdropConfiguration(_backdropConfig);
    }

    private static void CenterOnActiveMonitor(nint hwnd)
    {
        GetCursorPos(out var pt);
        var hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);

        var info = new MONITORINFO { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref info)) return;

        var work = info.rcWork;
        int x = work.left + (work.right - work.left - OverlayWidth) / 2;
        int y = work.top + (work.bottom - work.top - OverlayHeight) / 2;

        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);
        appWindow.Move(new PointInt32(x, y));
    }
}
