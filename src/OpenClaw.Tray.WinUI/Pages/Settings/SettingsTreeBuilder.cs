using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenClawTray.Pages.Settings;

/// <summary>
/// Variant C — Gateway-rooted hierarchy.
///
/// Builds a hierarchical settings tree that mirrors the actual domain model:
/// <c>Gateway → Nodes → ThisPC → ...</c>. The tree generation is data-driven
/// (loops over a node collection) so it scales to N paired nodes without UI
/// changes — today the user has one local node, tomorrow they may pair more.
///
/// The tree itself is a plain POCO graph; the XAML layer turns each
/// <see cref="SettingsTreeNode"/> into a <c>NavigationViewItem</c>. Keeping
/// data and view separate also makes the structure trivially unit-testable
/// without spinning up WinUI.
/// </summary>
internal static class SettingsTreeBuilder
{
    public sealed record SettingsTreeNode(
        string Tag,
        string Title,
        string Glyph,
        string? Subtitle = null,
        IReadOnlyList<SettingsTreeNode>? Children = null,
        bool IsExpandedByDefault = false)
    {
        public IReadOnlyList<SettingsTreeNode> Children { get; init; } =
            Children ?? Array.Empty<SettingsTreeNode>();
    }

    public sealed record NodeDescriptor(string Id, string DisplayName, string Glyph);

    /// <summary>
    /// Build the full tree given the active gateway display name and the set
    /// of nodes belonging to it. <paramref name="gatewayDisplay"/> may be
    /// null when no gateway is configured — we still render the structure so
    /// the user sees the IA and can drill into Connection to set one up.
    /// </summary>
    public static IReadOnlyList<SettingsTreeNode> Build(
        string? gatewayDisplay,
        IReadOnlyList<NodeDescriptor>? nodes)
    {
        nodes ??= Array.Empty<NodeDescriptor>();

        var gatewayLabel = string.IsNullOrWhiteSpace(gatewayDisplay)
            ? "Gateway · not configured"
            : $"Gateway · {gatewayDisplay}";

        var nodeChildren = nodes.Select(n => new SettingsTreeNode(
            Tag: $"node:{n.Id}",
            Title: n.DisplayName,
            Glyph: n.Glyph,
            Children: BuildNodeSubItems(n.Id))).ToArray();

        var gatewayChildren = new List<SettingsTreeNode>
        {
            new("connection",    "Connection",    "\uE839", "Pair, reconnect, or change gateway"),
            new("sessions",      "Sessions",      "\uE8F2", "Active sessions on this gateway"),
            new("conversations", "Conversations", "\uE8F2", "Past conversations and transcripts"),
            new("agentevents",   "Agent Events",  "\uE943", "Live agent event stream"),
            new("skills",        "Skills",        "\uE945", "Registered skills available to agents"),
            new("agents",        "Agents",        "\uE99A", "Configured agents and workspaces"),
            new("channels",      "Channels",      "\uEC05", "Gateway channel health"),
            new("bindings",      "Bindings",      "\uE8AD", "Channel and skill bindings"),
            new("config",        "Config",        "\uE90F", "Gateway configuration"),
            new("usage",         "Usage",         "\uE9D9", "Token, cost, and quota usage"),
            new("cron",          "Cron",          "\uE787", "Scheduled agent tasks"),
            new(
                Tag: "nodes",
                Title: "Nodes",
                Glyph: "\uE977",
                Subtitle: "Devices paired to this gateway",
                Children: nodeChildren,
                IsExpandedByDefault: true),
        };

        return new SettingsTreeNode[]
        {
            new("status",      "Status",      "\uE9D9", "Companion status overview"),
            new(
                Tag: "gateway",
                Title: gatewayLabel,
                Glyph: "\uE968",
                Subtitle: gatewayDisplay,
                Children: gatewayChildren,
                IsExpandedByDefault: true),
            new(
                Tag: "diagnostics",
                Title: "Diagnostics",
                Glyph: "\uE9D9",
                Children: new SettingsTreeNode[]
                {
                    new("debug", "Debug", "\uEBE8", "Logs, telemetry, diagnostics"),
                }),
            new("info", "About", "\uE946", "Version and about"),
        };
    }

    private static IReadOnlyList<SettingsTreeNode> BuildNodeSubItems(string nodeId)
    {
        // Per-node sub-items. Today only "this PC" exists, but the IDs are
        // namespaced so a future remote node would slot in cleanly.
        return new SettingsTreeNode[]
        {
            new($"{nodeId}:capabilities",    "Capabilities",     "\uE964", "Device capabilities advertised to gateway"),
            new($"{nodeId}:voice",           "Voice & Audio",    "\uE720", "Microphone and TTS preferences"),
            new($"{nodeId}:permissions",     "Permissions",      "\uE8D7", "Exec policy, allowlists, approvals"),
            new($"{nodeId}:sandbox",         "Sandbox",          "\uE72E", "Filesystem and command sandboxing"),
            new($"{nodeId}:activity",        "Activity",         "\uEA95", "Recent local activity"),
            new($"{nodeId}:apppreferences",  "App preferences",  "\uE713", "Startup, UI, notifications, privacy"),
        };
    }

    /// <summary>
    /// Extract a friendly host display from a gateway URL — e.g.
    /// <c>https://gateway.openclaw.dev:8443/</c> → <c>gateway.openclaw.dev</c>.
    /// </summary>
    public static string? FormatGatewayDisplay(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            var uri = new Uri(url);
            return uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
        }
        catch
        {
            return url;
        }
    }

    /// <summary>
    /// Walk the tree and return the breadcrumb path for <paramref name="tag"/>,
    /// or null if not found. Each entry is the node title.
    /// </summary>
    public static IReadOnlyList<string>? FindBreadcrumb(
        IReadOnlyList<SettingsTreeNode> roots, string tag)
    {
        var path = new List<string>();
        return Walk(roots, tag, path) ? path : null;
    }

    private static bool Walk(IReadOnlyList<SettingsTreeNode> nodes, string tag, List<string> path)
    {
        foreach (var n in nodes)
        {
            path.Add(n.Title);
            if (string.Equals(n.Tag, tag, StringComparison.OrdinalIgnoreCase)) return true;
            if (Walk(n.Children, tag, path)) return true;
            path.RemoveAt(path.Count - 1);
        }
        return false;
    }
}
