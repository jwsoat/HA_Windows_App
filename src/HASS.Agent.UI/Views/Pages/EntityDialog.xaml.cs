using HASS.Agent.Core.Entities;
using HASS.Agent.Shared.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HASS.Agent.UI.Views.Pages;

/// <summary>
/// Add/Edit dialog used by both Sensors and Commands pages.
/// Step 1: filterable type list. Step 2: dynamic form rendered from the chosen type's metadata.
/// </summary>
public sealed partial class EntityDialog : ContentDialog
{
    public enum Mode { Sensor, Command }

    private readonly Mode _mode;
    private readonly IReadOnlyList<EntityTypeMetadata> _allTypes;
    private readonly Dictionary<string, FrameworkElement> _fieldControls = new();

    /// <summary>Selected metadata after Save. Null on cancel.</summary>
    public EntityTypeMetadata? SelectedMeta { get; private set; }
    /// <summary>Field values after Save. Null on cancel.</summary>
    public Dictionary<string, string>? FieldValues { get; private set; }
    /// <summary>Name from the form.</summary>
    public string EnteredName { get; private set; } = string.Empty;
    /// <summary>Interval from the form (sensors only; for commands defaults to 0).</summary>
    public int EnteredInterval { get; private set; } = 30;
    /// <summary>Selected entity type (commands only).</summary>
    public CommandEntityType EnteredEntityType { get; private set; } = CommandEntityType.Button;

    /// <summary>Construct an Add dialog.</summary>
    public EntityDialog(Mode mode)
    {
        _mode = mode;
        _allTypes = mode == Mode.Sensor ? SensorTypeRegistry.All : CommandTypeRegistry.All;
        InitializeComponent();

        Title = mode == Mode.Sensor ? "Add Sensor" : "Add Command";
        ApplyFilter(string.Empty);

        if (mode == Mode.Command)
        {
            // Commands need an EntityType; sensors don't have one.
            EntityTypeCombo.Visibility = Visibility.Visible;
            foreach (var v in Enum.GetValues<CommandEntityType>())
                EntityTypeCombo.Items.Add(v.ToString());
            EntityTypeCombo.SelectedIndex = 0;
        }

        PrimaryButtonClick += OnPrimaryClick;
    }

    // ── Type picker ───────────────────────────────────────────────────────────

    private void OnFilterChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) =>
        ApplyFilter(sender.Text ?? string.Empty);

    private void ApplyFilter(string q)
    {
        q = q.Trim();
        TypeList.ItemsSource = string.IsNullOrEmpty(q)
            ? _allTypes
            : _allTypes.Where(m =>
                m.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.Description.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                m.Category.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnTypeSelected(object sender, SelectionChangedEventArgs e)
    {
        if (TypeList.SelectedItem is not EntityTypeMetadata meta) return;
        ShowEditForm(meta);
    }

    private void OnBackToPicker(object sender, RoutedEventArgs e)
    {
        StepTypePicker.Visibility = Visibility.Visible;
        StepEditForm.Visibility = Visibility.Collapsed;
        ErrorBar.IsOpen = false;
    }

    // ── Edit form ─────────────────────────────────────────────────────────────

    private void ShowEditForm(EntityTypeMetadata meta)
    {
        StepTypePicker.Visibility = Visibility.Collapsed;
        StepEditForm.Visibility = Visibility.Visible;
        SelectedTypeLabel.Text = meta.DisplayName;

        if (_mode == Mode.Sensor)
            IntervalBox.Value = meta.DefaultIntervalSeconds;

        // Build dynamic fields.
        DynamicFieldsHost.Children.Clear();
        _fieldControls.Clear();
        foreach (var f in meta.Fields)
        {
            FrameworkElement ctrl;
            switch (f.Kind)
            {
                case EntityFieldKind.MultilineText:
                    var multi = new TextBox
                    {
                        Header = f.Label, PlaceholderText = f.Placeholder,
                        AcceptsReturn = true, TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                        Height = 80, Text = f.DefaultValue
                    };
                    ctrl = multi;
                    break;
                case EntityFieldKind.Number:
                    var num = new NumberBox
                    {
                        Header = f.Label,
                        PlaceholderText = f.Placeholder,
                        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                        Value = double.TryParse(f.DefaultValue, out var dv) ? dv : 0
                    };
                    ctrl = num;
                    break;
                case EntityFieldKind.Boolean:
                    var tog = new ToggleSwitch
                    {
                        Header = f.Label,
                        IsOn = string.Equals(f.DefaultValue, "true", StringComparison.OrdinalIgnoreCase)
                    };
                    ctrl = tog;
                    break;
                case EntityFieldKind.KeyCodeList:
                    var keys = new TextBox
                    {
                        Header = f.Label, PlaceholderText = f.Placeholder, Text = f.DefaultValue
                    };
                    ctrl = keys;
                    break;
                default:
                    var tb = new TextBox
                    {
                        Header = f.Label, PlaceholderText = f.Placeholder, Text = f.DefaultValue
                    };
                    ctrl = tb;
                    break;
            }

            DynamicFieldsHost.Children.Add(ctrl);
            _fieldControls[f.Key] = ctrl;
        }
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    private void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (TypeList.SelectedItem is not EntityTypeMetadata meta)
        {
            args.Cancel = true;
            ShowError("Pick a type first.");
            return;
        }

        var name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            args.Cancel = true;
            ShowError("Name is required.");
            return;
        }

        var values = new Dictionary<string, string>();
        foreach (var f in meta.Fields)
        {
            if (!_fieldControls.TryGetValue(f.Key, out var ctrl)) continue;
            var raw = ctrl switch
            {
                TextBox t => t.Text?.Trim() ?? string.Empty,
                NumberBox n => ((int)n.Value).ToString(),
                ToggleSwitch tog => tog.IsOn ? "true" : "false",
                _ => string.Empty
            };

            if (f.Required && string.IsNullOrWhiteSpace(raw))
            {
                args.Cancel = true;
                ShowError($"\"{f.Label}\" is required.");
                return;
            }
            values[f.Key] = raw;
        }

        SelectedMeta = meta;
        FieldValues = values;
        EnteredName = name;
        EnteredInterval = (int)IntervalBox.Value;

        if (_mode == Mode.Command && Enum.TryParse<CommandEntityType>(
                EntityTypeCombo.SelectedItem?.ToString(), out var et))
            EnteredEntityType = et;
    }

    private void ShowError(string msg)
    {
        ErrorBar.Title = msg;
        ErrorBar.IsOpen = true;
    }
}
