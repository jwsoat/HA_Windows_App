using HASS.Agent.Core.Models.Internal;
using HASS.Agent.Shared.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HASS.Agent.UI.Controls;

public sealed partial class QuickActionCard : UserControl
{
    public static readonly DependencyProperty ActionProperty =
        DependencyProperty.Register(nameof(Action), typeof(QuickAction),
            typeof(QuickActionCard), new PropertyMetadata(null, OnActionChanged));

    public QuickAction? Action
    {
        get => (QuickAction?)GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    public QuickActionCard()
    {
        InitializeComponent();
    }

    private static void OnActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((QuickActionCard)d).Refresh((QuickAction?)e.NewValue);
    }

    private void Refresh(QuickAction? action)
    {
        if (action == null) return;

        NameText.Text = action.DisplayName;
        EntityText.Text = action.Entity;
        DomainIcon.Glyph = DomainToGlyph(action.Domain);
    }

    private static string DomainToGlyph(HassDomain domain) => domain switch
    {
        HassDomain.Light => "",        // Light bulb
        HassDomain.Switch => "",       // Toggle
        HassDomain.Script => "",       // Script/list
        HassDomain.Automation => "",   // Automation/lightning
        HassDomain.Scene => "",        // Scene/picture
        HassDomain.Cover => "",        // Cover/garage
        HassDomain.Climate => "",      // Climate/thermometer
        HassDomain.MediaPlayer => "",  // Media
        HassDomain.InputBoolean => "", // Toggle/checkbox
        _ => ""                        // Generic device
    };
}
