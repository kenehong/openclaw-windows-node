using System;
using System.Linq;
using System.Text.Json;
using OpenClaw.Shared;
using OpenClawTray.Helpers;
using OpenClawTray.Services;
using OpenClawTray.Services.Chat;
using Xunit;

namespace OpenClaw.Tray.Tests;

public class NativeChatTests
{
    // ── Feature flag ──

    [Fact]
    public void NativeChatFeature_DefaultsOff()
    {
        ClearEnv();
        var settings = NewIsolatedSettings();
        Assert.False(NativeChatFeature.IsEnabled(settings));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("YES", true)]
    [InlineData("on", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    public void NativeChatFeature_EnvVarOverridesSettings(string envValue, bool expected)
    {
        Environment.SetEnvironmentVariable("OPENCLAW_TRAY_NATIVE_CHAT", envValue);
        try
        {
            var settings = NewIsolatedSettings();
            settings.UseNativeChat = !expected; // env should override
            Assert.Equal(expected, NativeChatFeature.IsEnabled(settings));
        }
        finally
        {
            ClearEnv();
        }
    }

    [Fact]
    public void NativeChatFeature_FallsBackToSettingsWhenEnvUnset()
    {
        ClearEnv();
        var settings = NewIsolatedSettings();
        settings.UseNativeChat = true;
        Assert.True(NativeChatFeature.IsEnabled(settings));
        settings.UseNativeChat = false;
        Assert.False(NativeChatFeature.IsEnabled(settings));
    }

    private static void ClearEnv() =>
        Environment.SetEnvironmentVariable("OPENCLAW_TRAY_NATIVE_CHAT", null);

    private static SettingsManager NewIsolatedSettings()
    {
        // AGENTS.md rule: never construct against real %APPDATA%.
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "openclaw-tests-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        Environment.SetEnvironmentVariable("OPENCLAW_TRAY_DATA_DIR", dir);
        return new SettingsManager();
    }

    // ── ChatTranscriptStore ──

    private static AgentEventInfo Evt(string stream, string? runId = null, string? sessionKey = null, object? data = null)
    {
        var json = JsonSerializer.SerializeToElement(data ?? new { });
        return new AgentEventInfo
        {
            Stream = stream,
            RunId = runId ?? "",
            SessionKey = sessionKey,
            Data = json
        };
    }

    private static ChatTranscriptStore NewStore() => new(client: null, sessionKey: "main");

    [Fact]
    public void Apply_AssistantDeltas_CoalesceIntoSingleBubble()
    {
        var store = NewStore();
        store.Apply(Evt("lifecycle", "r1", data: new { state = "start" }));
        store.Apply(Evt("assistant", "r1", data: new { delta = "Hello " }));
        store.Apply(Evt("assistant", "r1", data: new { delta = "world." }));

        var asst = Assert.Single(store.Items.OfType<AssistantMessageItem>());
        Assert.Equal("Hello world.", asst.Text);
        Assert.True(asst.IsStreaming);
    }

    [Fact]
    public void Apply_LifecycleEnd_ClosesAssistantAndClearsActiveRun()
    {
        var store = NewStore();
        store.Apply(Evt("lifecycle", "r1", data: new { state = "start" }));
        store.Apply(Evt("assistant", "r1", data: new { delta = "Hi" }));
        store.Apply(Evt("lifecycle", "r1", data: new { state = "end" }));

        var asst = Assert.Single(store.Items.OfType<AssistantMessageItem>());
        Assert.False(asst.IsStreaming);
        Assert.Null(store.ActiveRunId);
    }

    [Fact]
    public void Apply_NoReplyDelta_IsFiltered()
    {
        var store = NewStore();
        store.Apply(Evt("lifecycle", "r1", data: new { state = "start" }));
        store.Apply(Evt("assistant", "r1", data: new { delta = "NO_REPLY" }));
        Assert.Empty(store.Items.OfType<AssistantMessageItem>());

        store.Apply(Evt("assistant", "r1", data: new { delta = "  no_reply  " }));
        Assert.Empty(store.Items.OfType<AssistantMessageItem>());

        // Non-NO_REPLY content still flows through.
        store.Apply(Evt("assistant", "r1", data: new { delta = "real" }));
        Assert.Single(store.Items.OfType<AssistantMessageItem>());
    }

    [Fact]
    public void Apply_ToolStartThenResult_MutatesCardInPlace()
    {
        var store = NewStore();
        store.Apply(Evt("lifecycle", "r1", data: new { state = "start" }));
        store.Apply(Evt("tool", "r1", data: new { name = "read", phase = "start", label = "read README.md" }));
        store.Apply(Evt("tool", "r1", data: new { name = "read", phase = "result", result = "1 file" }));

        var card = Assert.Single(store.Items.OfType<AgentEventCardItem>());
        Assert.Equal(AgentEventPhase.Done, card.Phase);
        Assert.Equal("1 file", card.Detail);
    }

    [Fact]
    public void Apply_MultipleToolsInterleave_KeepDistinctCards()
    {
        var store = NewStore();
        store.Apply(Evt("lifecycle", "r1", data: new { state = "start" }));
        store.Apply(Evt("tool", "r1", data: new { name = "read", phase = "start" }));
        store.Apply(Evt("tool", "r1", data: new { name = "search", phase = "start" }));
        store.Apply(Evt("tool", "r1", data: new { name = "read", phase = "result" }));

        var cards = store.Items.OfType<AgentEventCardItem>().ToList();
        Assert.Equal(2, cards.Count);
        Assert.Equal(AgentEventPhase.Done, cards.Single(c => c.ToolName == "read").Phase);
        Assert.Equal(AgentEventPhase.Running, cards.Single(c => c.ToolName == "search").Phase);
    }

    [Fact]
    public void Apply_ThinkingStream_RoutesToThinkingBlockNotAssistant()
    {
        var store = NewStore();
        store.Apply(Evt("lifecycle", "r1", data: new { state = "start" }));
        store.Apply(Evt("thinking", "r1", data: new { delta = "let me think" }));

        Assert.Empty(store.Items.OfType<AssistantMessageItem>());
        var thinking = Assert.Single(store.Items.OfType<ThinkingBlockItem>());
        Assert.Equal("let me think", thinking.Text);
    }

    [Fact]
    public void Apply_ErrorStream_AppendsSystemNotice()
    {
        var store = NewStore();
        store.Apply(Evt("error", data: new { message = "boom" }));
        var notice = Assert.Single(store.Items.OfType<SystemNoticeItem>());
        Assert.Equal(SystemNoticeKind.Error, notice.Kind);
        Assert.Equal("boom", notice.Message);
    }

    [Fact]
    public void Apply_RespectsSessionKeyFilter_WhenLiveClientNotUsed()
    {
        // The live event handler filters by session key. Here we exercise Apply directly
        // and verify it does *not* re-filter (Apply assumes the caller already routed).
        var store = NewStore();
        store.Apply(Evt("assistant", "r1", sessionKey: "other", data: new { delta = "hi" }));
        Assert.Single(store.Items.OfType<AssistantMessageItem>());
    }

    [Fact]
    public void IsNoReply_DetectsCommonForms()
    {
        Assert.True(ChatTranscriptStore.IsNoReply("NO_REPLY"));
        Assert.True(ChatTranscriptStore.IsNoReply("no_reply"));
        Assert.True(ChatTranscriptStore.IsNoReply("  No_Reply  "));
        Assert.False(ChatTranscriptStore.IsNoReply("hello"));
        Assert.False(ChatTranscriptStore.IsNoReply(""));
    }

    [Fact]
    public void ApplyHistory_HydratesUserAndAssistantMessages()
    {
        var store = NewStore();
        var json = JsonSerializer.SerializeToElement(new
        {
            messages = new object[]
            {
                new { role = "user", content = "ping" },
                new { role = "assistant", content = "pong" },
                new { role = "assistant", content = "NO_REPLY" } // filtered
            }
        });
        store.ApplyHistory(json);

        Assert.Equal(2, store.Items.Count);
        Assert.IsType<UserMessageItem>(store.Items[0]);
        Assert.IsType<AssistantMessageItem>(store.Items[1]);
    }
}
