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

public sealed partial class CommandsPage : Page
{
    private readonly IApplicationStateService _state;
    private readonly ISettingsService _settings;

    public CommandsPage()
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
        CommandsList.ItemsSource = _state.Commands;
        var n = _state.Commands.Count;
        CountText.Text = $"{n} command{(n == 1 ? "" : "s")}";
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        DeleteButton.IsEnabled = CommandsList.SelectedItem != null;

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        var dlg = new EntityDialog(EntityDialog.Mode.Command) { XamlRoot = XamlRoot };
        var result = await dlg.ShowAsync();
        if (result != ContentDialogResult.Primary || dlg.SelectedMeta == null) return;

        var cmd = EntityFactory.CreateCommand(
            dlg.SelectedMeta, dlg.EnteredName, dlg.EnteredEntityType,
            dlg.FieldValues ?? new Dictionary<string, string>());

        if (cmd == null)
        {
            await ShowError("Failed to create command — see logs for details.");
            return;
        }

        _state.Commands.Add(cmd);
        _settings.StoreCommands();
        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage());
        Refresh();
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (CommandsList.SelectedItem is not AbstractCommand cmd) return;

        var confirm = new ContentDialog
        {
            Title = "Delete Command",
            Content = $"Delete \"{cmd.Name}\"?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

        _state.Commands.Remove(cmd);
        _settings.StoreCommands();
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
