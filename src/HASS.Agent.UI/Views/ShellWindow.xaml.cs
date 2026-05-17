using CommunityToolkit.Mvvm.Messaging;
using HASS.Agent.Core.Services;
using HASS.Agent.UI.Services;
using HASS.Agent.UI.Views.Pages;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Graphics;
using WinRT;
using WinRT.Interop;

namespace HASS.Agent.UI.Views;

/// <summary>
/// Main settings shell window with Mica backdrop and left NavigationView.
/// Opened from the tray icon or the OpenSettingsMessage.
/// </summary>
public sealed partial class ShellWindow : Microsoft.UI.Xaml.Window
{
    private readonly NavigationService _nav;
    private MicaController? _micaController;
    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _backdropConfig;

    private static readonly Dictionary<string, Type> PageMap = new()
    {
        ["dashboard"]     = typeof(DashboardPage),
        ["quickactions"]  = typeof(QuickActionsPage),
        ["sensors"]       = typeof(SensorsPage),
        ["commands"]      = typeof(CommandsPage),
        ["mqtt"]          = typeof(MqttSettingsPage),
        ["hassapi"]       = typeof(HassApiSettingsPage),
        ["notifications"] = typeof(NotificationsPage),
        ["about"]         = typeof(AboutPage),
    };

    public ShellWindow(NavigationService nav)
    {
        _nav = nav;
        InitializeComponent();

        _nav.Initialize(ContentFrame);
        SetupWindow();
        SetupMicaBackdrop();

        // Navigate to Dashboard on open
        NavView.SelectedItem = NavView.MenuItems[0];
        _nav.Navigate(typeof(DashboardPage));
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item &&
            item.Tag is string tag &&
            PageMap.TryGetValue(tag, out var pageType))
        {
            _nav.Navigate(pageType);
        }
    }

    private void SetupWindow()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.Resize(new SizeInt32(1100, 720));
        appWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "hassagent.ico"));

        Title = "HASS.Agent";
    }

    private void SetupMicaBackdrop()
    {
        _backdropConfig = new SystemBackdropConfiguration { IsInputActive = true };

        // Track foreground state so the controller can dim the backdrop when unfocused.
        Activated += (_, e) =>
        {
            if (_backdropConfig != null)
                _backdropConfig.IsInputActive =
                    e.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated;
        };

        var backdropTarget = this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>();

        // Try Mica (Win11) → Acrylic (Win10 1903+) → no backdrop (very old / unsupported).
        if (MicaController.IsSupported())
        {
            _micaController = new MicaController { Kind = MicaKind.Base };
            _micaController.AddSystemBackdropTarget(backdropTarget);
            _micaController.SetSystemBackdropConfiguration(_backdropConfig);
            return;
        }

        if (DesktopAcrylicController.IsSupported())
        {
            _acrylicController = new DesktopAcrylicController();
            _acrylicController.AddSystemBackdropTarget(backdropTarget);
            _acrylicController.SetSystemBackdropConfiguration(_backdropConfig);
        }
        // else: WinUI falls back to its default solid theme brush.
    }
}
