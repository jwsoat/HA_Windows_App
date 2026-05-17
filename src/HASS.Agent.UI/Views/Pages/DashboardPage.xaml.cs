using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using HASS.Agent.Core.HomeAssistant;
using HASS.Agent.Core.Mqtt;
using HASS.Agent.Core.Services;
using HASS.Agent.UI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HASS.Agent.UI.Views.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly IApplicationStateService _state;
    private readonly IMqttService _mqtt;
    private readonly IHassApiService _hassApi;
    private readonly IUpdateCheckerService _updates;

    public DashboardPage()
    {
        _state   = App.Services.GetRequiredService<IApplicationStateService>();
        _mqtt    = App.Services.GetRequiredService<IMqttService>();
        _hassApi = App.Services.GetRequiredService<IHassApiService>();
        _updates = App.Services.GetRequiredService<IUpdateCheckerService>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Refresh();

        WeakReferenceMessenger.Default.Register<MqttConnectionStateMessage>(
            this, (_, msg) => DispatcherQueue.TryEnqueue(() => UpdateMqttStatus(msg.Value)));

        // Fire-and-forget GitHub release check; populates the InfoBar if a newer version exists.
        if (_state.AppSettings.CheckForUpdates)
            _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        var info = await _updates.CheckForUpdateAsync();
        if (info == null) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateInfoBar.Title = $"Update available — v{info.LatestVersion}";
            UpdateInfoBar.Message = $"You're running v{info.CurrentVersion}.";
            UpdateLink.NavigateUri = new Uri(info.ReleaseUrl);
            UpdateInfoBar.IsOpen = true;
        });
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private void Refresh()
    {
        QuickActionsCountText.Text =
            $"{_state.QuickActions.Count} action{(_state.QuickActions.Count == 1 ? "" : "s")} configured";

        // Reflect current MQTT + HA API state on every navigation
        UpdateMqttStatus(_mqtt.ConnectionState);
        UpdateHassApiStatus();
    }

    private void UpdateHassApiStatus()
    {
        var connected = !string.IsNullOrWhiteSpace(_hassApi.HaVersion);
        HassApiInfoBar.Severity = connected ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        HassApiInfoBar.Message = connected
            ? $"Connected — Home Assistant {_hassApi.HaVersion}"
            : "Not connected — open the HA API page to configure";
    }

    private void UpdateMqttStatus(MqttConnectionState state)
    {
        MqttInfoBar.Severity = state switch
        {
            MqttConnectionState.Connected    => InfoBarSeverity.Success,
            MqttConnectionState.Error        => InfoBarSeverity.Error,
            MqttConnectionState.Reconnecting => InfoBarSeverity.Warning,
            _                                => InfoBarSeverity.Informational
        };
        MqttInfoBar.Message = state switch
        {
            MqttConnectionState.NotConfigured => "Not configured — open the MQTT page to enable",
            MqttConnectionState.Disconnected  => "Disconnected",
            MqttConnectionState.Connecting    => "Connecting...",
            MqttConnectionState.Connected     => "Connected",
            MqttConnectionState.Reconnecting  => "Reconnecting...",
            MqttConnectionState.Error         => "Connection error",
            _                                 => state.ToString()
        };
    }

    private void OnOpenOverlay(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new QuickActionsHotkeyMessage());
    }
}
