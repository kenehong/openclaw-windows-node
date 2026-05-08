using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Models;
using OpenClawTray.PostOnboarding.Services;
using OpenClawTray.PostOnboarding.Widgets;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI.Xaml;

namespace OpenClawTray.PostOnboarding.Pages;

/// <summary>
/// Page 2 — Define the user's custom agent (name + avatar). Single
/// centered card (no left/right split). The card itself owns the
/// editable bits: clickable avatar, name input, Randomize button.
/// </summary>
public sealed class AgentDefinePage : Component<PostOnboardingState>
{
    public override Element Render()
    {
        var (agentName, setName) = UseState(Props.CustomAgentName);
        var (avatar, setAvatar) = UseState(string.IsNullOrEmpty(Props.CustomAgentAvatar)
            ? RandomAgentGenerator.AvatarPool[0]
            : Props.CustomAgentAvatar);

        if (string.IsNullOrEmpty(Props.CustomAgentAvatar))
        {
            Props.CustomAgentAvatar = avatar;
        }

        void Randomize()
        {
            var (n, a) = RandomAgentGenerator.Next();
            setName(n);
            setAvatar(a);
            Props.CustomAgentName = n;
            Props.CustomAgentAvatar = a;
        }

        var greetingName = string.IsNullOrWhiteSpace(Props.UserName) ? "there" : Props.UserName;

        var headline = VStack(4,
            TextBlock($"Hi {greetingName},").FontSize(28)
                .FontWeight(new global::Windows.UI.Text.FontWeight(700))
                .HAlign(HorizontalAlignment.Center),
            TextBlock("name me!").FontSize(28)
                .FontWeight(new global::Windows.UI.Text.FontWeight(700))
                .Opacity(0.55)
                .HAlign(HorizontalAlignment.Center)
        ).HAlign(HorizontalAlignment.Center);

        var card = AgentSetupCard.Render(
            agentName,
            avatar,
            value =>
            {
                setName(value);
                Props.CustomAgentName = value;
            },
            av =>
            {
                setAvatar(av);
                Props.CustomAgentAvatar = av;
            },
            Randomize);

        var content = VStack(28,
            headline,
            card.HAlign(HorizontalAlignment.Center)
        ).HAlign(HorizontalAlignment.Center);

        return Grid(["1*"], ["1*"],
            content.Grid(row: 0, column: 0)
                .HAlign(HorizontalAlignment.Center)
                .VAlign(VerticalAlignment.Center)
        ).Padding(40, 32, 40, 32);
    }
}
