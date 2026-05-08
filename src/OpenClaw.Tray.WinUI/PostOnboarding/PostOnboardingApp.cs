using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.FunctionalUI.Navigation;
using OpenClawTray.PostOnboarding.Models;
using OpenClawTray.PostOnboarding.Pages;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI.Xaml;

namespace OpenClawTray.PostOnboarding;

/// <summary>
/// Root component for the post-onboarding wizard.
/// Owns navigation between four pages, plus the persistent step indicator
/// and Back/Next/Finish nav bar.
/// </summary>
public sealed class PostOnboardingApp : Component<PostOnboardingState>
{
    private static readonly PostOnboardingRoute[] Pages =
    {
        PostOnboardingRoute.UserName,
        PostOnboardingRoute.AgentDefine,
        PostOnboardingRoute.AgentPick,
        PostOnboardingRoute.AgentChat,
    };

    public override Element Render()
    {
        var (pageIndex, setPageIndex) = UseState(0);
        var nav = UseNavigation(Pages[pageIndex]);

        void GoNext()
        {
            if (pageIndex < Pages.Length - 1)
            {
                var newIdx = pageIndex + 1;
                setPageIndex(newIdx);
                nav.Navigate(Pages[newIdx]);
                Props.NotifyPageChanged();
            }
        }

        void GoBack()
        {
            if (pageIndex > 0)
            {
                var newIdx = pageIndex - 1;
                setPageIndex(newIdx);
                nav.GoBack();
                Props.NotifyPageChanged();
            }
        }

        bool CanGoNext()
        {
            // We let users always advance — validation is handled per-page,
            // and the Skip button is always available too. The TextField
            // doesn't trigger a parent re-render on each keystroke, so a
            // disabled-Next pattern would lag behind typing.
            return true;
        }

        var isLastPage = pageIndex >= Pages.Length - 1;

        var content = NavigationHost<PostOnboardingRoute>(nav, route => route switch
        {
            PostOnboardingRoute.UserName => Component<UserNamePage, PostOnboardingState>(Props),
            PostOnboardingRoute.AgentDefine => Component<AgentDefinePage, PostOnboardingState>(Props),
            PostOnboardingRoute.AgentPick => Component<AgentPickPage, PostOnboardingState>(Props),
            PostOnboardingRoute.AgentChat => Component<AgentChatPage, PostOnboardingState>(Props),
            _ => TextBlock("Unknown page"),
        }) with
        {
            Transition = NavigationTransition.SlideInOnly(
                direction: SlideDirection.FromRight,
                duration: TimeSpan.FromMilliseconds(280),
                distance: 60),
        };

        var navBar = Grid(["Auto", "1*", "Auto", "Auto"], ["Auto"],
            Button("Back", GoBack).Disabled(pageIndex <= 0).Width(96)
                .Grid(row: 0, column: 0).HAlign(HorizontalAlignment.Left),
            StepIndicator(Pages.Length, pageIndex)
                .Grid(row: 0, column: 1).HAlign(HorizontalAlignment.Center).VAlign(VerticalAlignment.Center),
            Button("Skip", Props.Complete)
                .Width(96).Grid(row: 0, column: 2).Margin(0, 0, 12, 0),
            Button(isLastPage ? "Finish" : "Next",
                isLastPage ? Props.Complete : GoNext)
                .Disabled(!CanGoNext())
                .Width(96).Grid(row: 0, column: 3)
        ).Padding(24, 12, 24, 16);

        return Grid(["1*"], ["1*", "Auto"],
            ((Element)content).Grid(row: 0, column: 0),
            navBar.Grid(row: 1, column: 0)
        );
    }

    private static Element StepIndicator(int total, int current)
    {
        var dots = Enumerable.Range(0, total).Select(i =>
            Border().Width(8).Height(8).CornerRadius(4)
                .Set(b => b.Background = i == current
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black)
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(
                        Microsoft.UI.ColorHelper.FromArgb(60, 0, 0, 0)))
        ).Cast<Element>().ToArray();

        return HStack(6, dots).VAlign(VerticalAlignment.Center);
    }
}
