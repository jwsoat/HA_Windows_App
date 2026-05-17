using CommunityToolkit.Mvvm.Messaging;
using HASS.Agent.Core.Configuration;
using HASS.Agent.Core.Entities;
using HASS.Agent.Core.Services;
using HASS.Agent.Shared.Models.HomeAssistant;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HASS.Agent.UI.Views.Pages;

public sealed partial class SensorsPage : Page
{
    private readonly IApplicationStateService _state;
    private readonly ISettingsService _settings;

    public SensorsPage()
    {
        _state = App.Services.GetRequiredService<IApplicationStateService>();
        _settings = App.Services.GetRequiredService<ISettingsService>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Refresh();
    }

    private void Refresh()
    {
        var all = new List<object>();
        all.AddRange(_state.SingleValueSensors);
        all.AddRange(_state.MultiValueSensors);
        SensorsList.ItemsSource = all;

        var n = all.Count;
        CountText.Text = $"{n} sensor{(n == 1 ? "" : "s")}";
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        DeleteButton.IsEnabled = SensorsList.SelectedItem != null;

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        var dlg = new EntityDialog(EntityDialog.Mode.Sensor) { XamlRoot = XamlRoot };
        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary || dlg.SelectedMeta == null) return;

        var sensor = EntityFactory.CreateSensor(
            dlg.SelectedMeta, dlg.EnteredName, dlg.EnteredInterval,
            dlg.FieldValues ?? new Dictionary<string, string>());

        if (sensor == null)
        {
            await ShowError("Failed to create sensor — see logs for details.");
            return;
        }

        if (dlg.SelectedMeta.IsMultiValue && sensor is AbstractMultiValueSensor mv)
            _state.MultiValueSensors.Add(mv);
        else if (sensor is AbstractSingleValueSensor sv)
            _state.SingleValueSensors.Add(sv);

        _settings.StoreSensors();
        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage());
        Refresh();
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (SensorsList.SelectedItem is null) return;
        var item = SensorsList.SelectedItem;

        var name = item switch
        {
            AbstractSingleValueSensor sv => sv.Name,
            AbstractMultiValueSensor mv => mv.Name,
            _ => "this sensor"
        };

        var confirm = new ContentDialog
        {
            Title = "Delete Sensor",
            Content = $"Delete \"{name}\"?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        if (item is AbstractSingleValueSensor svRemove)
            _state.SingleValueSensors.Remove(svRemove);
        else if (item is AbstractMultiValueSensor mvRemove)
            _state.MultiValueSensors.Remove(mvRemove);

        _settings.StoreSensors();
        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage());
        Refresh();
    }

    private async Task ShowError(string msg)
    {
        var dlg = new ContentDialog
        {
            Title = "Error",
            Content = msg,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };
        await dlg.ShowAsync();
    }
}
