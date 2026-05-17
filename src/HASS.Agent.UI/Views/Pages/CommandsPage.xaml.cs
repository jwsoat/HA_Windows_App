using Microsoft.Extensions.DependencyInjection;
using HASS.Agent.Core.Services;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HASS.Agent.UI.Views.Pages;

public sealed partial class CommandsPage : Page
{
    private readonly IApplicationStateService _state;

    public CommandsPage()
    {
        _state = App.Services.GetRequiredService<IApplicationStateService>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        CommandsList.ItemsSource = _state.Commands;
    }
}
