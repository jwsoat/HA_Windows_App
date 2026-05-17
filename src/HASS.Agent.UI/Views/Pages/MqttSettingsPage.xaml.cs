using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using HASS.Agent.Core.Configuration;
using HASS.Agent.Core.Mqtt;
using HASS.Agent.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HASS.Agent.UI.Views.Pages;

public sealed partial class MqttSettingsPage : Page
{
    private readonly IApplicationStateService _state;
    private readonly ISettingsService _settings;
    private readonly IMqttService _mqtt;
    private readonly MqttCredentialVault _vault;

    public MqttSettingsPage()
    {
        _state   = App.Services.GetRequiredService<IApplicationStateService>();
        _settings = App.Services.GetRequiredService<ISettingsService>();
        _mqtt    = App.Services.GetRequiredService<IMqttService>();
        _vault   = App.Services.GetRequiredService<MqttCredentialVault>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = _state.AppSettings;
        EnabledToggle.IsOn            = s.MqttEnabled;
        AddressBox.Text               = s.MqttAddress;
        PortBox.Value                 = s.MqttPort;
        TlsToggle.IsOn                = s.MqttUseTls;
        TlsToggle.Toggled            += (_, _) => UpdateTlsDependents();
        AllowUntrustedToggle.IsOn     = s.MqttAllowUntrustedCertificates;
        UpdateTlsDependents();
        UsernameBox.Text              = s.MqttUsername;
        ClientIdBox.Text              = s.MqttClientId;
        DiscoveryPrefixBox.Text       = s.MqttDiscoveryPrefix;
        RetainFlagToggle.IsOn         = s.MqttUseRetainFlag;

        if (!string.IsNullOrWhiteSpace(s.MqttUsername))
        {
            var (_, pass) = _vault.Retrieve(s.MqttUsername);
            PasswordBox.Password = pass ?? string.Empty;
        }
    }

    private void UpdateTlsDependents() =>
        AllowUntrustedToggle.Visibility = TlsToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;

    private async void OnTestConnection(object sender, RoutedEventArgs e)
    {
        TestResultBar.IsOpen = false;
        var ok = await _mqtt.CheckConnectionAsync();
        TestResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        TestResultBar.Message  = ok ? "Connection successful" : "Connection failed";
        TestResultBar.IsOpen   = true;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var s = _state.AppSettings;
        s.MqttEnabled                    = EnabledToggle.IsOn;
        s.MqttAddress                    = AddressBox.Text.Trim();
        s.MqttPort                       = (int)PortBox.Value;
        s.MqttUseTls                     = TlsToggle.IsOn;
        s.MqttAllowUntrustedCertificates = AllowUntrustedToggle.IsOn;
        s.MqttUsername                   = UsernameBox.Text.Trim();
        s.MqttClientId                   = ClientIdBox.Text.Trim();
        s.MqttDiscoveryPrefix            = DiscoveryPrefixBox.Text.Trim();
        s.MqttUseRetainFlag              = RetainFlagToggle.IsOn;

        if (!string.IsNullOrWhiteSpace(s.MqttUsername) && !string.IsNullOrWhiteSpace(PasswordBox.Password))
            _vault.MigrateFromPlaintext(s.MqttUsername, PasswordBox.Password);

        _settings.StoreAppSettings();
        WeakReferenceMessenger.Default.Send(new SettingsChangedMessage());
        await _mqtt.ReloadConfigurationAsync();
    }
}
