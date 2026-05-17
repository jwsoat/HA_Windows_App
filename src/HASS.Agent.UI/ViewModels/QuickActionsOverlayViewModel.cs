using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HASS.Agent.Core.HomeAssistant;
using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Core.Services;
using Serilog;

namespace HASS.Agent.UI.ViewModels;

/// <summary>Observable model for one group filter pill.</summary>
public partial class GroupPill : ObservableObject
{
    public string Name { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isActive;
}

public partial class QuickActionsOverlayViewModel : ObservableObject
{
    private readonly IHassApiService _hassApi;
    private readonly IApplicationStateService _state;

    // ── Observable state ──────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredActions))]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<GroupPill> Groups { get; } = new();
    public ObservableCollection<QuickAction> FilteredActions { get; } = new();

    private List<QuickAction> _allActions = new();
    private string _activeGroup = "All";

    public QuickActionsOverlayViewModel(IHassApiService hassApi, IApplicationStateService state)
    {
        _hassApi = hassApi;
        _state = state;
    }

    public void Load()
    {
        _allActions = _state.QuickActions
            .Where(a => a.IsEnabled)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.DisplayName)
            .ToList();

        Groups.Clear();
        Groups.Add(new GroupPill { Name = "All", IsActive = true });
        foreach (var g in _allActions
            .Select(a => a.GroupName)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct()
            .OrderBy(g => g))
        {
            Groups.Add(new GroupPill { Name = g! });
        }

        _activeGroup = "All";
        ApplyFilter();
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ExecuteActionAsync(QuickAction action)
    {
        if (action == null || IsBusy) return;
        IsBusy = true;
        try
        {
            if (action.IsSequence)
                await ExecuteSequenceAsync(action);
            else
                await _hassApi.ProcessQuickActionAsync(action);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[QA] Error executing action {name}: {err}", action.DisplayName, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectGroup(string group)
    {
        _activeGroup = group;
        foreach (var pill in Groups)
            pill.IsActive = pill.Name == group;
        ApplyFilter();
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private async Task ExecuteSequenceAsync(QuickAction action)
    {
        foreach (var step in action.Sequence!)
        {
            var stepAction = new QuickAction
            {
                Domain = step.Domain,
                Entity = step.Entity,
                Action = step.Action
            };
            await _hassApi.ProcessQuickActionAsync(stepAction);
            if (step.DelayAfterMs > 0)
                await Task.Delay(step.DelayAfterMs);
        }
    }

    private void ApplyFilter()
    {
        var filtered = _allActions.AsEnumerable();

        if (_activeGroup != "All")
            filtered = filtered.Where(a => a.GroupName == _activeGroup);

        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(a =>
                a.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                a.Entity.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        FilteredActions.Clear();
        foreach (var a in filtered)
            FilteredActions.Add(a);
    }
}
