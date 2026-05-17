using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using HASS.Agent.Core.HomeAssistant;
using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Core.Services;
using HASS.Agent.Shared.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HASS.Agent.UI.Views.Pages;

public sealed partial class QuickActionDialog : ContentDialog
{
    public QuickAction? Result { get; private set; }

    private readonly ObservableCollection<QuickActionSequenceStep> _steps = new();
    private readonly IHassApiService _hassApi;
    private readonly QuickAction? _editing;

    public QuickActionDialog(QuickAction? existing = null)
    {
        _hassApi = App.Services.GetRequiredService<IHassApiService>();
        _editing = existing;
        InitializeComponent();

        PopulateDomain();
        PopulateAction();
        PopulateEntities();
        SequenceList.ItemsSource = _steps;

        if (existing != null) LoadExisting(existing);

        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void PopulateDomain()
    {
        foreach (var d in Enum.GetValues<HassDomain>())
            DomainCombo.Items.Add(d.ToString());
        DomainCombo.SelectedIndex = 0;
        DomainCombo.SelectionChanged += (_, _) => PopulateEntities();
    }

    private void PopulateAction()
    {
        foreach (var a in Enum.GetValues<HassAction>())
            ActionCombo.Items.Add(a.ToString());
        ActionCombo.SelectedIndex = 0;
    }

    private void PopulateEntities()
    {
        EntityCombo.Items.Clear();
        if (DomainCombo.SelectedItem is not string domainStr) return;
        if (!Enum.TryParse<HassDomain>(domainStr, out var domain)) return;

        var list = domain switch
        {
            HassDomain.Light       => _hassApi.LightList,
            HassDomain.Switch      => _hassApi.SwitchList,
            HassDomain.Automation  => _hassApi.AutomationList,
            HassDomain.Script      => _hassApi.ScriptList,
            HassDomain.Scene       => _hassApi.SceneList,
            HassDomain.Cover       => _hassApi.CoverList,
            HassDomain.Climate     => _hassApi.ClimateList,
            HassDomain.MediaPlayer => _hassApi.MediaPlayerList,
            HassDomain.InputBoolean=> _hassApi.InputBooleanList,
            _                      => new List<string>()
        };

        foreach (var e in list) EntityCombo.Items.Add(e);
    }

    private void LoadExisting(QuickAction qa)
    {
        DescriptionBox.Text              = qa.Description;
        EntityCombo.Text                 = qa.Entity;
        DomainCombo.SelectedItem         = qa.Domain.ToString();
        ActionCombo.SelectedItem         = qa.Action.ToString();
        GroupBox.Text                    = qa.GroupName ?? string.Empty;
        EnabledToggle.IsOn               = qa.IsEnabled;
        ShowStateToggle.IsOn             = qa.ShowEntityState;
        HotkeyBox.Text                   = qa.HotKey;
        HotkeyEnabledToggle.IsOn         = qa.HotKeyEnabled;

        if (qa.Sequence != null)
            foreach (var step in qa.Sequence)
                _steps.Add(step);
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!Enum.TryParse<HassDomain>(DomainCombo.SelectedItem?.ToString(), out var domain)) domain = HassDomain.Light;
        if (!Enum.TryParse<HassAction>(ActionCombo.SelectedItem?.ToString(), out var action)) action = HassAction.Toggle;

        var qa = _editing ?? new QuickAction();
        qa.Description   = DescriptionBox.Text.Trim();
        qa.Entity        = EntityCombo.Text.Trim();
        qa.Domain        = domain;
        qa.Action        = action;
        qa.GroupName     = string.IsNullOrWhiteSpace(GroupBox.Text) ? null : GroupBox.Text.Trim();
        qa.IsEnabled     = EnabledToggle.IsOn;
        qa.ShowEntityState = ShowStateToggle.IsOn;
        qa.HotKey        = HotkeyBox.Text.Trim();
        qa.HotKeyEnabled = HotkeyEnabledToggle.IsOn;
        qa.Sequence      = _steps.Count > 0 ? _steps.ToList() : null;

        Result = qa;
    }

    private void OnAddStep(object sender, RoutedEventArgs e)
    {
        _steps.Add(new QuickActionSequenceStep
        {
            Domain = HassDomain.Light,
            Action = HassAction.Toggle
        });
    }

    private void OnRemoveStep(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: QuickActionSequenceStep step })
            _steps.Remove(step);
    }
}
