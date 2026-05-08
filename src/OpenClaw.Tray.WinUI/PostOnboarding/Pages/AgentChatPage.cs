using OpenClawTray.FunctionalUI;
using OpenClawTray.FunctionalUI.Core;
using OpenClawTray.PostOnboarding.Models;
using OpenClawTray.PostOnboarding.Services;
using OpenClawTray.Controls;
using static OpenClawTray.FunctionalUI.Factories;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace OpenClawTray.PostOnboarding.Pages;

/// <summary>
/// Page 4 — Chat surface with a centered agent picker (‹ Avatar Name ›),
/// a tagline, and a fan of suggested-prompt cards as the zero-state. The
/// production <see cref="ChatShell"/> composer sits below. Sending a
/// message hides the zero-state and shows the message transcript.
/// </summary>
public sealed class AgentChatPage : Component<PostOnboardingState>
{
    public override Element Render()
    {
        var custom = new PrebakedAgent(
            AgentKind.Custom,
            "custom",
            string.IsNullOrWhiteSpace(Props.CustomAgentName) ? "Your Agent" : Props.CustomAgentName,
            string.IsNullOrWhiteSpace(Props.CustomAgentName) ? "You" : Props.CustomAgentName,
            "Your personal assistant",
            string.IsNullOrEmpty(Props.CustomAgentAvatar) ? "🙂" : Props.CustomAgentAvatar,
            new[] { "Custom" });

        var pool = new List<PrebakedAgent> { custom };
        pool.AddRange(Props.SelectedPrebakedAgentIds
            .Select(PrebakedAgentCatalog.FindById)
            .Where(a => a is not null)
            .Select(a => a!));

        var initialActive = Math.Max(0, pool.FindIndex(a => a.Id == Props.ActiveAgentId));
        if (pool.Count > 0)
        {
            Props.ActiveAgentId = pool[initialActive].Id;
        }

        var capturedProps = Props;
        return NativeXaml(() => BuildChatHost(pool, initialActive, capturedProps));
    }

    private static FrameworkElement BuildChatHost(
        List<PrebakedAgent> pool, int initialIdx, PostOnboardingState props)
    {
        int currentIdx = pool.Count == 0 ? 0 : initialIdx;
        PrebakedAgent Current() => pool[currentIdx];

        // Zero-state stage (centered picker + tagline + fan cards).
        var stageHost = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };

        // Transcript (hidden until first user send).
        var transcript = new StackPanel
        {
            Spacing = 10,
            Padding = new Thickness(20, 16, 20, 16),
        };
        var scroller = new ScrollViewer
        {
            Content = transcript,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Disabled,
            Visibility = Visibility.Collapsed,
        };

        var threadGrid = new Microsoft.UI.Xaml.Controls.Grid();
        threadGrid.Children.Add(stageHost);
        threadGrid.Children.Add(scroller);

        var shell = new ChatShell
        {
            ThreadContent = threadGrid,
            IsHeaderVisible = false,
        };

        async void Send(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // First send: hide stage, show transcript.
            stageHost.Visibility = Visibility.Collapsed;
            scroller.Visibility = Visibility.Visible;

            var a = Current();
            transcript.Children.Add(MakeUserBubble(text));
            ScrollToBottom(scroller);

            var typing = MakeAgentBubble(a, "…");
            transcript.Children.Add(typing);
            ScrollToBottom(scroller);

            try
            {
                var reply = await props.Backend.GetReplyAsync(
                    a.Kind, a.DisplayName, text, default);
                transcript.Children.Remove(typing);
                transcript.Children.Add(MakeAgentBubble(a, reply));
                ScrollToBottom(scroller);
            }
            catch
            {
                transcript.Children.Remove(typing);
            }
        }

        void Step(int delta)
        {
            if (pool.Count == 0) return;
            currentIdx = ((currentIdx + delta) % pool.Count + pool.Count) % pool.Count;
            props.ActiveAgentId = pool[currentIdx].Id;
            // Always reset to the new agent's stage when switching.
            transcript.Children.Clear();
            scroller.Visibility = Visibility.Collapsed;
            stageHost.Visibility = Visibility.Visible;
            stageHost.Content = BuildStage(Current(), Step, Send);
        }

        shell.SendRequested += (_, text) => Send(text);

        if (pool.Count > 0)
        {
            stageHost.Content = BuildStage(Current(), Step, Send);
        }

        return shell;
    }

    /// <summary>
    /// Builds the zero-state stage: ‹ avatar + name › on top, tagline below,
    /// then a fan of three suggested-prompt cards.
    /// </summary>
    private static FrameworkElement BuildStage(
        PrebakedAgent agent, Action<int> step, Action<string> send)
    {
        // Centered picker row: ‹  [avatar] Name  ›
        var prevBtn = MakeChevronButton("\uE76B", () => step(-1)); // left chevron
        var nextBtn = MakeChevronButton("\uE76C", () => step(1));  // right chevron

        var avatar = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 0xE6, 0xE6, 0xE6)),
            Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(agent.Avatar) ? "🙂" : agent.Avatar,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var nameText = new TextBlock
        {
            Text = agent.DisplayName,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var center = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        center.Children.Add(avatar);
        center.Children.Add(nameText);

        var pickerRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        pickerRow.Children.Add(prevBtn);
        pickerRow.Children.Add(center);
        pickerRow.Children.Add(nextBtn);

        var tagline = new TextBlock
        {
            Text = TaglineFor(agent.Kind),
            FontSize = 13,
            Opacity = 0.6,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 480,
        };

        // Fan of cards.
        var fan = MakeCardFan(SuggestedPrompts(agent.Kind), send);

        var col = new StackPanel
        {
            Spacing = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        col.Children.Add(pickerRow);
        col.Children.Add(tagline);
        col.Children.Add(fan);

        return new Microsoft.UI.Xaml.Controls.Grid
        {
            Children = { col },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(24, 32, 24, 24),
        };
    }

    private static Button MakeChevronButton(string glyph, Action onClick)
    {
        var btn = new Button
        {
            Content = new FontIcon { Glyph = glyph, FontSize = 14 },
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(36, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    /// <summary>
    /// Renders suggested-prompt pills in a centered, wrapping row.
    /// </summary>
    private static FrameworkElement MakeCardFan(
        IReadOnlyList<(string Title, string Desc)> prompts, Action<string> send)
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        foreach (var p in prompts)
        {
            panel.Children.Add(MakePromptPill(p.Title, () => send(p.Title)));
        }
        return panel;
    }

    private static Button MakePromptPill(string title, Action onClick)
    {
        var btn = new Button
        {
            Content = new TextBlock
            {
                Text = title,
                FontSize = 13,
                TextWrapping = TextWrapping.NoWrap,
            },
            Padding = new Thickness(16, 8, 16, 8),
            CornerRadius = new CornerRadius(20),
            Background = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"],
            BorderBrush = new SolidColorBrush(ColorHelper.FromArgb(36, 0, 0, 0)),
            BorderThickness = new Thickness(1),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button MakePromptCard(string title, string desc, Action onClick)
    {
        // Retained for compatibility; pills are used now.
        return MakePromptPill(title, onClick);
    }

    private static void ScrollToBottom(ScrollViewer scroller)
    {
        scroller.UpdateLayout();
        scroller.ChangeView(null, scroller.ScrollableHeight, null, disableAnimation: false);
    }

    private static FrameworkElement MakeUserBubble(string text)
    {
        return new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 8, 12, 8),
            MaxWidth = 480,
            HorizontalAlignment = HorizontalAlignment.Right,
            Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = new SolidColorBrush(Colors.White),
            },
        };
    }

    private static FrameworkElement MakeAgentBubble(PrebakedAgent agent, string text)
    {
        var avatar = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(14),
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(ColorHelper.FromArgb(255, 0xE6, 0xE6, 0xE6)),
            Child = new TextBlock
            {
                Text = string.IsNullOrEmpty(agent.Avatar) ? "🙂" : agent.Avatar,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };

        var bubble = new Border
        {
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(12, 8, 12, 8),
            MaxWidth = 480,
            Background = (Brush)Application.Current.Resources["ControlFillColorDefaultBrush"],
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
            },
        };

        var name = new TextBlock
        {
            Text = agent.DisplayName,
            FontSize = 11,
            Opacity = 0.6,
            Margin = new Thickness(0, 0, 0, 2),
        };

        var col = new StackPanel { Spacing = 0 };
        col.Children.Add(name);
        col.Children.Add(bubble);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        row.Children.Add(avatar);
        row.Children.Add(col);
        return row;
    }

    private static string TaglineFor(AgentKind kind) => kind switch
    {
        AgentKind.Coder => "Pair-programming, refactors & code review",
        AgentKind.SingerSongwriter => "Lyrics, arrangement inspiration & music style analysis",
        AgentKind.VanGogh => "Visual ideas, scene composition & art direction",
        AgentKind.LifeMaster => "Daily planning, habits & balance",
        AgentKind.GrowthHacker => "Funnels, experiments & growth tactics",
        AgentKind.MoneyLeopard => "Markets, portfolios & financial briefings",
        _ => "Your personal assistant",
    };

    private static IReadOnlyList<(string Title, string Desc)> SuggestedPrompts(AgentKind kind) => kind switch
    {
        AgentKind.Coder => new (string, string)[]
        {
            ("Build a TODO app", "Scaffold a small React + TS sample with state and persistence."),
            ("Refactor this function", "Improve readability, naming and split out side-effects."),
            ("Explain async/await", "Walk through the event loop and common pitfalls."),
        },
        AgentKind.SingerSongwriter => new (string, string)[]
        {
            ("Recommend arrangement inspiration", "Suggest reference tracks and chord progressions by mood."),
            ("Analyze this song's style", "Break down genre, arrangement choices and emotional arc."),
            ("Write some lyrics for me", "Create original lyrics based on theme and emotion."),
        },
        AgentKind.VanGogh => new (string, string)[]
        {
            ("Paint a sunset over the sea", "Compose a warm-toned seascape with bold brushwork."),
            ("Describe a starry night", "Spin an impressionist scene with motion in the sky."),
            ("Sketch a self-portrait", "Plan composition, palette and expressive marks."),
        },
        AgentKind.LifeMaster => new (string, string)[]
        {
            ("Plan my day", "Block deep work, breaks and a finishing ritual."),
            ("Suggest a 20-min workout", "Bodyweight circuit you can do in a small room."),
            ("Recipe with what's in my fridge", "Tell me what you have, I'll plan a meal."),
        },
        AgentKind.GrowthHacker => new (string, string)[]
        {
            ("Growth tactics for SaaS", "Pull a list of low-cost experiments to try this week."),
            ("Optimize my landing page", "Audit headline, CTA and above-the-fold structure."),
            ("Brainstorm viral campaigns", "10 angles tailored to your audience and product."),
        },
        AgentKind.MoneyLeopard => new (string, string)[]
        {
            ("Scan AAPL fundamentals", "Snapshot of valuation, growth and recent moves."),
            ("Explain ETFs in 1 minute", "Plain-English primer with examples."),
            ("Build a dividend portfolio", "Sketch allocation across sectors and yields."),
        },
        _ => new (string, string)[]
        {
            ("What can you do?", "A quick tour of how I can help."),
            ("Help me brainstorm", "Toss out a topic and I'll riff with you."),
            ("Plan something with me", "Describe the goal — I'll structure the steps."),
        },
    };
}
