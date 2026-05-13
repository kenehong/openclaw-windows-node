using Microsoft.UI.Xaml.Controls;
using OpenClawTray.Windows;

namespace OpenClawTray.Pages.Settings;

/// <summary>
/// Variant C-1 — minimal page hosting <see cref="SettingsStatusCard"/> for the
/// "status" tree node. Replaces the overview pane that previously lived inside
/// the now-removed <c>SettingsHostPage</c>.
/// </summary>
public sealed partial class StatusPage : Page
{
    public StatusPage()
    {
        InitializeComponent();
    }

    public void Initialize(HubWindow hub)
    {
        StatusCard.UseCompactLayout();
        StatusCard.Initialize(hub);
    }
}
