using OpenClawTray.PostOnboarding.Models;
using OpenClawTray.PostOnboarding.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinUIEx;

namespace OpenClawTray.PostOnboarding;

/// <summary>
/// Standalone host window for the post-onboarding POC.
/// Launched via the <c>--post-onboarding</c> CLI flag or from HubWindow.
/// </summary>
public sealed class PostOnboardingWindow : WindowEx
{
    public event EventHandler? CompletedAndClosed;

    private readonly PostOnboardingShell _shell;

    public PostOnboardingWindow()
        : this(new PostOnboardingState(new MockAgentChatBackend()))
    {
    }

    public PostOnboardingWindow(PostOnboardingState state)
    {
        Title = "Post-Onboarding (POC)";
        this.SetWindowSize(1280, 880);
        this.CenterOnScreen();
        try { this.SetIcon("Assets\\openclaw.ico"); } catch { /* dev runs may not have icon */ }
        SystemBackdrop = new MicaBackdrop();

        _shell = new PostOnboardingShell(state);
        _shell.Completed += (_, _) =>
        {
            CompletedAndClosed?.Invoke(this, EventArgs.Empty);
            try { Close(); } catch { /* already closing */ }
        };

        var root = new Grid
        {
            Background = (SolidColorBrush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"],
        };
        root.Children.Add(_shell);
        Content = root;
    }
}
