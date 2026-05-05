using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClawTray.Windows;

namespace OpenClawTray.Pages;

public sealed partial class ChatPage : Page
{
    private HubWindow? _hub;

    public ChatPage()
    {
        InitializeComponent();
    }

    public void Initialize(HubWindow hub)
    {
        _hub = hub;
        if (hub.Settings != null)
        {
            var url = hub.Settings.GetEffectiveGatewayUrl();
            var token = hub.Settings.Token ?? string.Empty;
            if (!string.IsNullOrEmpty(url))
            {
                Surface.Initialize(url, token);
            }
        }
    }

    private void OnHome(object sender, RoutedEventArgs e) => Surface.NavigateHome();
    private void OnRefresh(object sender, RoutedEventArgs e) => Surface.Reload();
    private void OnPopout(object sender, RoutedEventArgs e) => Surface.OpenInBrowser();
    private void OnDevTools(object sender, RoutedEventArgs e) => Surface.OpenDevTools();
}
