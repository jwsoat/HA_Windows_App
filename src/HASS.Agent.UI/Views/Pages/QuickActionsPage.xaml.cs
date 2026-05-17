using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using HASS.Agent.Core.Configuration;
using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HASS.Agent.UI.Views.Pages;

public sealed partial class QuickActionsPage : Page
{
    public ObservableCollection<QuickAction> Actions { get; } = new();

    private readonly IApplicationStateService _state;
    private readonly ISettingsService _settings;

    public QuickActionsPage()
    {
        _state    = App.Services.GetRequiredService<IApplicationStateService>();
        _settings = App.Services.GetRequiredService<ISettingsService>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Reload();
    }

    private void Reload()
    {
        Actions.Clear();
        foreach (var qa in _state.QuickActions.OrderBy(a => a.SortOrder))
            Actions.Add(qa);
        UpdateCount();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = ActionsList.SelectedItem != null;
        EditButton.IsEnabled   = hasSelection;
        DeleteButton.IsEnabled = hasSelection;
        MoveUpButton.IsEnabled = hasSelection && ActionsList.SelectedIndex > 0;
        MoveDownButton.IsEnabled = hasSelection && ActionsList.SelectedIndex < Actions.Count - 1;
    }

    private async void OnAdd(object sender, RoutedEventArgs e)
    {
        var dialog = new QuickActionDialog { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result != null)
        {
            dialog.Result.SortOrder = Actions.Count;
            Actions.Add(dialog.Result);
            SaveAsync();
        }
    }

    private async void OnEdit(object sender, RoutedEventArgs e)
    {
        if (ActionsList.SelectedItem is not QuickAction action) return;
        var dialog = new QuickActionDialog(action) { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            SaveAsync();
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (ActionsList.SelectedItem is not QuickAction action) return;

        var confirm = new ContentDialog
        {
            Title = "Delete Quick Action",
            Content = $"Delete \"{action.DisplayName}\"?",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            Actions.Remove(action);
            SaveAsync();
        }
    }

    private async void OnMoveUp(object sender, RoutedEventArgs e)
    {
        var idx = ActionsList.SelectedIndex;
        if (idx <= 0) return;
        Actions.Move(idx, idx - 1);
        ActionsList.SelectedIndex = idx - 1;
        SaveAsync();
    }

    private async void OnMoveDown(object sender, RoutedEventArgs e)
    {
        var idx = ActionsList.SelectedIndex;
        if (idx >= Actions.Count - 1) return;
        Actions.Move(idx, idx + 1);
        ActionsList.SelectedIndex = idx + 1;
        SaveAsync();
    }

    private void SaveAsync()
    {
        // Re-apply sort order from current list position
        for (int i = 0; i < Actions.Count; i++)
            Actions[i].SortOrder = i;

        _state.QuickActions.Clear();
        foreach (var a in Actions)
            _state.QuickActions.Add(a);

        _settings.StoreQuickActions();
        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage());
        UpdateCount();
    }

    private void UpdateCount() =>
        CountText.Text = $"{Actions.Count} action{(Actions.Count == 1 ? "" : "s")}";
}
