using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Services;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenClawTray.PostOnboarding.Widgets;

/// <summary>
/// Centered "set up your agent" card used on Page 2 (editable) and as the
/// read-only preview on Page 3 (with optional org-chart slot below).
///
/// Composition (top → bottom, all centered):
///   • Big avatar (96px)
///   • "PERSONAL ASSISTANT" caption + green online dot
///   • Agent name (TextBox when editable, TextBlock when preview)
///   • Avatar picker grid (only when editable)
///   • 🎲 Randomize button (only when editable)
///   • Optional org-chart slot (Page 3 only)
/// </summary>
public static class AgentSetupCard
{
    public static Element Render(
        string agentName,
        string avatar,
        Action<string>? onNameChanged,
        Action<string>? onAvatarChanged,
        Action? onRandomize,
        Element? orgChart = null)
    {
        var editable = onNameChanged is not null
            && onAvatarChanged is not null
            && onRandomize is not null;

        var displayName = string.IsNullOrWhiteSpace(agentName) ? "Your Agent" : agentName;
        var safeAvatar = string.IsNullOrEmpty(avatar) ? "🙂" : avatar;

        var bigAvatar = AgentArtworkResolver.Avatar(safeAvatar, 96, displayName)
            .HAlign(HorizontalAlignment.Center);

        var roleRow = HStack(6,
            TextBlock("PERSONAL ASSISTANT").FontSize(11).Opacity(0.55)
                .FontWeight(new global::Windows.UI.Text.FontWeight(600))
                .VAlign(VerticalAlignment.Center),
            Border().Width(8).Height(8).CornerRadius(4)
                .Set(b => b.Background = new SolidColorBrush(
                    ColorHelper.FromArgb(255, 0x4C, 0xAF, 0x50)))
                .VAlign(VerticalAlignment.Center)
        ).HAlign(HorizontalAlignment.Center);

        Element nameElement = editable
            ? (Element)TextField(agentName, onNameChanged!, placeholder: "e.g. Toby")
                .Width(280)
                .HAlign(HorizontalAlignment.Center)
                .Set(tb =>
                {
                    tb.TextAlignment = TextAlignment.Center;
                    tb.FontSize = 22;
                    tb.FontWeight = new global::Windows.UI.Text.FontWeight(700);
                })
            : TextBlock(displayName).FontSize(22)
                .FontWeight(new global::Windows.UI.Text.FontWeight(700))
                .HAlign(HorizontalAlignment.Center);

        var children = new List<Element?>
        {
            bigAvatar,
            roleRow,
            nameElement,
        };

        if (editable)
        {
            children.Add(AvatarPicker.Render(safeAvatar, onAvatarChanged!)
                .HAlign(HorizontalAlignment.Center));
            children.Add(Button("🎲  Randomize", onRandomize!)
                .HAlign(HorizontalAlignment.Center));
        }

        if (orgChart is not null)
        {
            children.Add(orgChart);
        }

        var stack = VStack(16, children.ToArray()).HAlign(HorizontalAlignment.Center);

        return Border(stack.Padding(28))
            .CornerRadius(16)
            .BackgroundResource("CardBackgroundFillColorDefaultBrush")
            .Set(b =>
            {
                b.BorderThickness = new Thickness(1);
                b.BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(40, 0, 0, 0));
            })
            .Width(420);
    }
}
