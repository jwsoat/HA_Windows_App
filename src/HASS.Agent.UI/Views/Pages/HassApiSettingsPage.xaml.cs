using Microsoft.Extensions.DependencyInjection;
using HASS.Agent.Core.Configuration;
using HASS.Agent.Core.HomeAssistant;
using HASS.Agent.Core.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HASS.Agent.UI.Views.Pages;

public sealed partial class HassApiSettingsPage : Page
{
    private readonly IApplicationStateService _state;
    private readonly ISettingsService _settings;
    private readonly IHassApiService _hassApi;

    public HassApiSettingsPage()
    {
        _state   = App.Services.GetRequiredService<IApplicationStateService>();
        _settings = App.Services.GetRequiredService<ISettingsService>();
        _hassApi = App.Services.GetRequiredService<IHassApiService>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var s = _state.AppSettings;
        UriBox.Text = s.HassUri;
        TokenBox.Password = s.HassToken;
        AllowUntrustedToggle.IsOn = s.HassAllowUntrustedCertificates;

        if (!string.IsNullOrWhiteSpace(_hassApi.HaVersion))
        {
            ShowInfo();
        }
    }

    private async void OnTest(object sender, RoutedEventArgs e)
    {
        StatusBar.IsOpen = false;
        var ok = await _hassApi.CheckConnectionAsync();
        StatusBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        StatusBar.Message  = ok ? $"Connected — HA {_hassApi.HaVersion}" : "Connection failed";
        StatusBar.IsOpen   = true;
        if (ok) ShowInfo();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var s = _state.AppSettings;
        s.HassUri = UriBox.Text.Trim();
        s.HassToken = TokenBox.Password;
        s.HassAllowUntrustedCertificates = AllowUntrustedToggle.IsOn;
        _settings.StoreAppSettings();
    }

    private void ShowInfo()
    {
        VersionText.Text = $"HA Version: {_hassApi.HaVersion}";
        EntityCountText.Text = $"Entities: {
            _hassApi.LightList.Count + _hassApi.SwitchList.Count +
            _hassApi.AutomationList.Count + _hassApi.ScriptList.Count +
            _hassApi.SceneList.Count} loaded";
        InfoPanel.Visibility = Visibility.Visible;
    }
}
