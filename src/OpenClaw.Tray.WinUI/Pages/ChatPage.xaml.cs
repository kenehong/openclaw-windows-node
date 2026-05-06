using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenClawTray.Helpers;
using OpenClawTray.Windows;

namespace OpenClawTray.Pages;

public sealed partial class ChatPage : Page
{
    private HubWindow? _hub;
    private bool _nativeActive;

    public ChatPage()
    {
        InitializeComponent();
    }

    public void Initialize(HubWindow hub)
    {
        _hub = hub;
        _nativeActive = NativeChatFeature.IsEnabled(hub.Settings);

        if (_nativeActive)
        {
            Surface.Visibility = Visibility.Collapsed;
            NativeSurface.Visibility = Visibility.Visible;
            if (hub.GatewayClient != null)
            {
                var url = hub.Settings?.GetEffectiveGatewayUrl() ?? string.Empty;
                var token = hub.Settings?.Token ?? string.Empty;
                NativeSurface.Initialize(url, token, hub.GatewayClient);
            }
            return;
        }

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

    private void OnHome(object sender, RoutedEventArgs e)
    {
        if (_nativeActive) NativeSurface.NavigateHome(); else Surface.NavigateHome();
    }
    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (_nativeActive) NativeSurface.Reload(); else Surface.Reload();
    }
    private void OnPopout(object sender, RoutedEventArgs e)
    {
        if (_nativeActive) NativeSurface.OpenInBrowser(); else Surface.OpenInBrowser();
    }
    private void OnDevTools(object sender, RoutedEventArgs e)
    {
        if (_nativeActive) NativeSurface.OpenDevTools(); else Surface.OpenDevTools();
    }
}
