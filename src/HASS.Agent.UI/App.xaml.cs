using CommunityToolkit.Mvvm.Messaging;
using HASS.Agent.Core.Configuration;
using HASS.Agent.Core.HomeAssistant;
using HASS.Agent.Core.Hotkeys;
using HASS.Agent.Core.Mqtt;
using HASS.Agent.Core.Services;
using HASS.Agent.UI.Services;
using HASS.Agent.UI.ViewModels;
using HASS.Agent.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.UI.Xaml;
using Serilog;

namespace HASS.Agent.UI;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    private MainWindow? _mainWindow;

    public App()
    {
        InitializeComponent();
        ConfigureLogging();
        UnhandledException += (_, e) =>
        {
            Log.Fatal(e.Exception, "[APP] Unhandled exception: {err}", e.Exception?.Message);
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var services = new ServiceCollection();

        services
            .AddSingleton<IApplicationStateService, ApplicationStateService>()
            .AddSingleton<ISettingsService, SettingsService>()
            .AddSingleton<MqttCredentialVault>()
            .AddSingleton<IMqttService, MqttService>()
            .AddSingleton<IHassApiService, HassApiService>()
            .AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>()
            .AddSingleton<TrayIconService>()
            .AddSingleton<NavigationService>()
            .AddSingleton<QuickActionsOverlayViewModel>()
            .AddSingleton<ShellWindow>()
            .AddSingleton<MainWindow>();


        Services = services.BuildServiceProvider();

        _mainWindow = Services.GetRequiredService<MainWindow>();
        _mainWindow.Activate();

        _ = Task.Run(StartServicesAsync);
    }

    private static async Task StartServicesAsync()
    {
        try
        {
            var settings = Services.GetRequiredService<ISettingsService>();
            await settings.LoadAsync();

            var state = Services.GetRequiredService<IApplicationStateService>();
            Log.Information("[APP] Starting HASS.Agent {device}", state.AppSettings.DeviceName);

            // Notify MainWindow that settings are loaded — triggers tray icon + hotkey registration
            WeakReferenceMessenger.Default.Send(new SettingsChangedMessage());

            var mqtt = Services.GetRequiredService<IMqttService>();
            await mqtt.InitializeAsync();

            var hass = Services.GetRequiredService<IHassApiService>();
            await hass.InitializeAsync();

            Log.Information("[APP] Core services initialized");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "[APP] Fatal error during startup: {err}", ex.Message);
        }
    }

    private static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LAB02Research", "HASS.Agent", "logs", "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
    }
}
