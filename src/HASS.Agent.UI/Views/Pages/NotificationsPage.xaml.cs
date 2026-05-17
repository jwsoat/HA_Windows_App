using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HASS.Agent.UI.Views.Pages;

public sealed partial class NotificationsPage : Page
{
    public NotificationsPage() => InitializeComponent();

    private void OnSendTest(object sender, RoutedEventArgs e)
    {
        // TODO: send a test notification via the notification manager
    }
}
