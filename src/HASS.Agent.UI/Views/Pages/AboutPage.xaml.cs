using System.Reflection;
using Microsoft.UI.Xaml.Controls;

namespace HASS.Agent.UI.Views.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        var ver = Assembly.GetEntryAssembly()?.GetName().Version;
        VersionText.Text = ver != null
            ? $"Version {ver.Major}.{ver.Minor}.{ver.Build}"
            : "Version unknown";
    }
}
