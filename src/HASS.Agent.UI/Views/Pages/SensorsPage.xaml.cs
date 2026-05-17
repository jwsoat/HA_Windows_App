using Microsoft.Extensions.DependencyInjection;
using HASS.Agent.Core.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HASS.Agent.UI.Views.Pages;

public sealed partial class SensorsPage : Page
{
    private readonly IApplicationStateService _state;

    public SensorsPage()
    {
        _state = App.Services.GetRequiredService<IApplicationStateService>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        SensorsList.ItemsSource = _state.SingleValueSensors;
    }
}
