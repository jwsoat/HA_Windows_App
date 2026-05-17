using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
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

    public DashboardPage()
    {
        _state = App.Services.GetRequiredService<IApplicationStateService>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Refresh();

        WeakReferenceMessenger.Default.Register<MqttConnectionStateMessage>(
            this, (_, msg) => DispatcherQueue.TryEnqueue(() => UpdateMqttStatus(msg.Value)));
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
        MqttInfoBar.Message = state.ToString();
    }

    private void OnOpenOverlay(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new QuickActionsHotkeyMessage());
    }
}
