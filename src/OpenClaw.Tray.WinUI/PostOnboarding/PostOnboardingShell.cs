using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Hosting;
using OpenClawTray.PostOnboarding.Models;
using OpenClawTray.PostOnboarding.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace OpenClawTray.PostOnboarding;

/// <summary>
/// Reusable embeddable wrapper for the post-onboarding flow. Wraps a
/// <see cref="FunctionalHostControl"/> hosting <see cref="PostOnboardingApp"/>
/// so the same content can be dropped into ComponentLibraryWindow for
/// design comparison or used as the root of a standalone window.
/// </summary>
public sealed class PostOnboardingShell : ContentControl
{
    public PostOnboardingState State { get; }

    /// <summary>
    /// Raised when the user reaches the Finish button on the last page.
    /// Hosts (window or component library) can use this to close / reset.
    /// </summary>
    public event EventHandler? Completed
    {
        add => State.Finished += value;
        remove => State.Finished -= value;
    }

    public PostOnboardingShell()
        : this(new PostOnboardingState(new MockAgentChatBackend()))
    {
    }

    public PostOnboardingShell(PostOnboardingState state)
    {
        State = state;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;

        var host = new FunctionalHostControl();
        host.Mount(ctx =>
        {
            var (s, _) = ctx.UseState(State);
            return Factories.Component<PostOnboardingApp, PostOnboardingState>(s);
        });

        Content = host;
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }
}
