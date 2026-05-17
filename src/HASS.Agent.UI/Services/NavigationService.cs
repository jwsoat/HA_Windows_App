using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HASS.Agent.UI.Services;

/// <summary>
/// Simple page navigation helper for the Shell NavigationView.
/// </summary>
public class NavigationService
{
    private Frame? _frame;

    public bool CanGoBack => _frame?.CanGoBack ?? false;

    public void Initialize(Frame frame) => _frame = frame;

    public bool Navigate(Type pageType, object? parameter = null)
    {
        if (_frame == null) return false;

        // Don't re-navigate to the same page type
        if (_frame.CurrentSourcePageType == pageType) return false;

        return _frame.Navigate(pageType, parameter);
    }

    public void GoBack()
    {
        if (_frame?.CanGoBack == true)
            _frame.GoBack();
    }

    public event NavigatedEventHandler? Navigated
    {
        add { if (_frame != null) _frame.Navigated += value; }
        remove { if (_frame != null) _frame.Navigated -= value; }
    }
}
