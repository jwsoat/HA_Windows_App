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
        if (!MicaController.IsSupported()) return;

        _backdropConfig = new SystemBackdropConfiguration
        {
            IsInputActive = true
        };

        _micaController = new MicaController { Kind = MicaKind.Base };
        _micaController.AddSystemBackdropTarget(
            this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
        _micaController.SetSystemBackdropConfiguration(_backdropConfig);

        Activated += (_, e) =>
            _backdropConfig.IsInputActive =
                e.WindowActivationState != Microsoft.UI.Xaml.WindowActivationState.Deactivated;
    }
}
