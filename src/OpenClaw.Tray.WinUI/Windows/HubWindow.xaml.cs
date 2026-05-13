using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenClaw.Shared;
using OpenClawTray.Helpers;
using OpenClawTray.Pages;
using OpenClawTray.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using WinUIEx;

namespace OpenClawTray.Windows;

public sealed partial class HubWindow : WindowEx
{
    public bool IsClosed { get; private set; }

    // Shared state accessible by pages
    private SettingsManager? _settings;
    public SettingsManager? Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            // Variant C-1: pane is overlay-only (LeftMinimal). We no longer
            // restore IsPaneOpen from settings — overlay panes always start
            // closed and the user opens them on demand via the hamburger.
        }
    }
    public IOperatorGatewayClient? GatewayClient { get; set; }
    public ConnectionStatus CurrentStatus { get; set; }
    private string _currentAgentId = "main";
    public string CurrentAgentId => _currentAgentId;

    // Legacy compatibility alias
    public string SelectedAgentId => _currentAgentId;
    public Action<string?>? OpenDashboardAction { get; set; }
    public Action? CheckForUpdatesAction { get; set; }
    public Action? ConnectAction { get; set; }
    public Action? DisconnectAction { get; set; }
    public Action? ReconnectAction { get; set; }
    public Action? OpenSetupAction { get; set; }
    public Action? OpenConnectionStatusAction { get; set; }
    public Action? OpenVoiceAction { get; set; }
    public OpenClawTray.Services.Connection.IGatewayConnectionManager? ConnectionManager { get; set; }
    public OpenClawTray.Services.Connection.GatewayRegistry? GatewayRegistry { get; set; }

    // Node service state (set by App.xaml.cs in ShowHub)
    public bool NodeIsConnected { get; set; }
    public bool NodeIsPaired { get; set; }
    public bool NodeIsPendingApproval { get; set; }
    public string? LastAuthError { get; set; }
    public string? NodeShortDeviceId { get; set; }
    public VoiceService? VoiceServiceInstance { get; set; }
    public string? NodeFullDeviceId { get; set; }

    // Cached gateway data — pages read these on navigation
    public SessionInfo[]? LastSessions { get; private set; }
    public ChannelHealth[]? LastChannels { get; private set; }
    public GatewayUsageInfo? LastUsage { get; private set; }
    public GatewayCostUsageInfo? LastUsageCost { get; private set; }
    public GatewayUsageStatusInfo? LastUsageStatus { get; private set; }
    public GatewayNodeInfo[]? LastNodes { get; private set; }

    public System.Text.Json.JsonElement? LastConfig { get; private set; }
    public System.Text.Json.JsonElement? LastConfigSchema { get; private set; }

    // Event for settings saved (App.xaml.cs subscribes)
    public event EventHandler? SettingsSaved;

    public void RaiseSettingsSaved() => SettingsSaved?.Invoke(this, EventArgs.Empty);

    public HubWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Closed += (s, e) => IsClosed = true;

        this.SetWindowSize(900, 650);
        this.CenterOnScreen();
        this.SetIcon(IconHelper.GetStatusIconPath(ConnectionStatus.Connected));

        RootGrid.SizeChanged += OnRootGridSizeChanged;

        // Variant C-1: build the hamburger-overlay menu (Home + full C tree
        // from SettingsTreeBuilder). Settings/GatewayClient may not be set
        // yet, so we re-build later when the gateway display name is known.
        BuildNavMenu();

        // Don't select a nav item here — Settings/GatewayClient aren't set yet.
        // ShowHub() in App.xaml.cs calls NavigateToDefault() after setting properties.
    }

    private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Overlay pane: keep a comfortable width regardless of window width.
        const double minPane = 240;
        const double maxPane = 320;
        double desired = e.NewSize.Width * 0.30;
        NavView.OpenPaneLength = Math.Clamp(desired, minPane, maxPane);
    }

    private void OnHamburgerClick(object sender, RoutedEventArgs e)
    {
        if (NavView == null) return;
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    // ── Variant C-1: hamburger-overlay nav driven by SettingsTreeBuilder ──

    /// <summary>
    /// Tag → NavigationViewItem index used to (a) reflect deep-link navigation
    /// in the overlay pane and (b) avoid scanning the tree on every selection.
    /// Rebuilt whenever <see cref="BuildNavMenu"/> runs.
    /// </summary>
    private readonly Dictionary<string, NavigationViewItem> _navItemsByTag =
        new(StringComparer.OrdinalIgnoreCase);

    private bool _suppressNavSelection;

    /// <summary>
    /// (Re)build the overlay menu: Home (Chat) + separator + full C tree from
    /// <see cref="OpenClawTray.Pages.Settings.SettingsTreeBuilder"/>. Branch
    /// nodes that don't map to a page are marked non-selectable so they only
    /// expand/collapse on click. Idempotent.
    /// </summary>
    public void BuildNavMenu()
    {
        if (NavView == null) return;

        // Preserve the hidden AgentsNavItem (used by RebuildAgentNavItems).
        NavigationViewItem? agentsHost = null;
        foreach (var existing in NavView.MenuItems)
        {
            if (existing is NavigationViewItem nv && ReferenceEquals(nv, AgentsNavItem))
            {
                agentsHost = nv;
                break;
            }
        }

        NavView.MenuItems.Clear();
        _navItemsByTag.Clear();

        // Home (Chat) — root entry.
        var homeItem = new NavigationViewItem
        {
            Content = "Home",
            Tag = "home",
            Icon = new FontIcon { Glyph = "\uE8BD" }
        };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(homeItem, "HubNav_home");
        NavView.MenuItems.Add(homeItem);
        _navItemsByTag["home"] = homeItem;

        NavView.MenuItems.Add(new NavigationViewItemSeparator());

        // Full C tree.
        var gatewayDisplay = OpenClawTray.Pages.Settings.SettingsTreeBuilder.FormatGatewayDisplay(
            GatewayRegistry?.GetActive()?.Url ?? Settings?.GetEffectiveGatewayUrl());

        var nodes = new[]
        {
            new OpenClawTray.Pages.Settings.SettingsTreeBuilder.NodeDescriptor(
                Id: "thispc",
                DisplayName: $"This PC ({Environment.MachineName})",
                Glyph: "\uE977"),
        };

        var roots = OpenClawTray.Pages.Settings.SettingsTreeBuilder.Build(gatewayDisplay, nodes);
        foreach (var root in roots)
        {
            NavView.MenuItems.Add(BuildNavItemFromTree(root));
        }

        // Re-attach hidden agents host so RebuildAgentNavItems still works.
        if (agentsHost != null) NavView.MenuItems.Add(agentsHost);
    }

    private NavigationViewItem BuildNavItemFromTree(
        OpenClawTray.Pages.Settings.SettingsTreeBuilder.SettingsTreeNode node)
    {
        var item = new NavigationViewItem
        {
            Content = node.Title,
            Tag = node.Tag,
            Icon = new FontIcon { Glyph = node.Glyph },
            IsExpanded = node.IsExpandedByDefault,
        };
        if (!string.IsNullOrEmpty(node.Subtitle))
            ToolTipService.SetToolTip(item, node.Subtitle);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(item, $"HubNav_{node.Tag}");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(item, node.Title);

        // Branch parents that don't resolve to a page only expand/collapse.
        if (node.Children.Count > 0 && TagToPageType(node.Tag) == null)
        {
            item.SelectsOnInvoked = false;
        }

        foreach (var child in node.Children)
            item.MenuItems.Add(BuildNavItemFromTree(child));

        _navItemsByTag[node.Tag] = item;
        return item;
    }

    /// <summary>
    /// Navigate to the default page. Call after setting Settings/GatewayClient.
    /// </summary>
    public void NavigateToDefault()
    {
        // Re-build menu so the gateway display name (now known) is shown.
        BuildNavMenu();
        if (ContentFrame.Content == null)
        {
            NavigateTo("home");
        }
    }

    /// <summary>
    /// Navigate to a specific page by tag name (e.g. "home", "sessions",
    /// "channels"). Variant C-1: also selects the matching item in the
    /// hamburger-overlay tree so deep-link entry points stay consistent.
    /// </summary>
    public void NavigateTo(string tag)
    {
        // Map legacy / alias tags.
        if (string.Equals(tag, "general", StringComparison.OrdinalIgnoreCase)) tag = "home";
        if (string.Equals(tag, "settings", StringComparison.OrdinalIgnoreCase)) tag = "status";
        if (string.Equals(tag, "about", StringComparison.OrdinalIgnoreCase)) tag = "info";
        // Map bare per-node sub-tags onto the local node namespace.
        if (LooksLikeNodeSubTag(tag)) tag = $"thispc:{tag}";
        // Legacy agent-scoped fallbacks.
        if (string.Equals(tag, "cron", StringComparison.OrdinalIgnoreCase))
            tag = $"agent:{_currentAgentId}:cron";
        if (string.Equals(tag, "workspace", StringComparison.OrdinalIgnoreCase))
            tag = $"agent:{_currentAgentId}:workspace";

        if (tag.StartsWith("agent:", StringComparison.OrdinalIgnoreCase))
        {
            _currentAgentId = ParseAgentIdFromTag(tag);
            _cachedCommands = null;
        }

        var pageType = TagToPageType(tag);
        if (pageType != null)
        {
            ContentFrame.Navigate(pageType);
            InitializeCurrentPage();
        }

        // Reflect the navigation in the overlay tree (and auto-close overlay).
        SelectNavItem(tag);
    }

    private static bool LooksLikeNodeSubTag(string tag) => tag.ToLowerInvariant() switch
    {
        "capabilities" or "voice" or "permissions" or "sandbox" or
        "activity" or "apppreferences" => true,
        _ => false
    };

    /// <summary>
    /// Set <see cref="NavigationView.SelectedItem"/> to the item matching
    /// <paramref name="tag"/> without re-triggering <see cref="NavView_SelectionChanged"/>.
    /// Auto-expands ancestor branch items and closes the overlay so the
    /// content swap feels instant.
    /// </summary>
    private void SelectNavItem(string tag)
    {
        if (!_navItemsByTag.TryGetValue(tag, out var item)) return;
        _suppressNavSelection = true;
        try
        {
            ExpandAncestorsOf(item);
            if (NavView.SelectedItem != item) NavView.SelectedItem = item;
        }
        finally { _suppressNavSelection = false; }

        // Selecting any leaf closes the overlay (Variant C-1 contract).
        if (NavView.IsPaneOpen) NavView.IsPaneOpen = false;
    }

    private void ExpandAncestorsOf(NavigationViewItem item)
    {
        // Walk MenuItems tree; mark every ancestor of `item` as IsExpanded.
        foreach (var root in NavView.MenuItems)
        {
            if (root is NavigationViewItem nv && ExpandIfAncestor(nv, item))
                return;
        }
    }

    private static bool ExpandIfAncestor(NavigationViewItem candidate, NavigationViewItem target)
    {
        if (ReferenceEquals(candidate, target)) return true;
        foreach (var child in candidate.MenuItems)
        {
            if (child is NavigationViewItem cnv && ExpandIfAncestor(cnv, target))
            {
                candidate.IsExpanded = true;
                return true;
            }
        }
        return false;
    }

    public void UpdateStatus(ConnectionStatus status)
    {
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                CurrentStatus = status;
                _cachedCommands = null;
                if (status == ConnectionStatus.Disconnected)
                    _lastGatewaySelf = null;
                UpdateTitleBarStatus(status);
                if (ContentFrame?.Content is HomePage homePage)
                {
                    homePage.UpdateConnectionStatus(status, Settings?.GetEffectiveGatewayUrl());
                }
                if (ContentFrame?.Content is ConnectionPage connectionPage)
                {
                    connectionPage.UpdateStatus(status);
                }
            });
        }
        catch { }
    }

    private void UpdateTitleBarStatus(ConnectionStatus status)
    {
        var (color, text) = status switch
        {
            ConnectionStatus.Connected => (Microsoft.UI.Colors.LimeGreen, "Connected"),
            ConnectionStatus.Connecting => (Microsoft.UI.Colors.Orange, "Connecting…"),
            ConnectionStatus.Error => (Microsoft.UI.Colors.Red, "Error"),
            _ => (Microsoft.UI.Colors.Gray, "Disconnected")
        };

        TitleStatusDot.Fill = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);
        TitleStatusText.Text = text;

        // Add gateway version if available
        if (status == ConnectionStatus.Connected && GatewayClient != null)
        {
            var self = _lastGatewaySelf;
            if (self != null && !string.IsNullOrEmpty(self.ServerVersion))
                TitleStatusText.Text = $"Connected · v{self.ServerVersion}";
            if (self?.PresenceCount is > 0)
                TitleStatusText.Text += $" · {self.PresenceCount} clients";
        }
    }

    private GatewaySelfInfo? _lastGatewaySelf;
    public GatewaySelfInfo? LastGatewaySelf => _lastGatewaySelf;

    public void UpdateGatewaySelf(GatewaySelfInfo self)
    {
        _lastGatewaySelf = self;
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                UpdateTitleBarStatus(CurrentStatus);
                if (ContentFrame?.Content is AboutPage about)
                    about.RefreshGatewayInfo();
            });
        }
        catch { }
    }

    public void UpdateSessions(SessionInfo[] sessions)
    {
        LastSessions = sessions;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is SessionsPage sp) sp.UpdateSessions(sessions);
            else if (ContentFrame?.Content is ConversationsPage convos) convos.UpdateSessions(sessions);
            else if (ContentFrame?.Content is HomePage home) home.UpdateSessions(sessions);
        });
    }

    public void UpdateChannelHealth(ChannelHealth[] channels)
    {
        LastChannels = channels;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is ChannelsPage cp) cp.UpdateChannels(channels);
        });
    }

    public void UpdateUsage(GatewayUsageInfo usage)
    {
        LastUsage = usage;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is UsagePage up) up.UpdateUsage(usage);
        });
    }

    public void UpdateUsageCost(GatewayCostUsageInfo cost)
    {
        LastUsageCost = cost;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is UsagePage up) up.UpdateUsageCost(cost);
        });
    }

    public void UpdateUsageStatus(GatewayUsageStatusInfo status)
    {
        LastUsageStatus = status;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is UsagePage up) up.UpdateUsageStatus(status);
        });
    }

    public void UpdateNodes(GatewayNodeInfo[] nodes)
    {
        LastNodes = nodes;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is NodesPage np) np.UpdateNodes(nodes);
            else if (ContentFrame?.Content is HomePage home) home.UpdateNodes(nodes);
        });
    }

    // Cached cron data for when CronPage isn't active
    private System.Text.Json.JsonElement? _lastCronList;
    private System.Text.Json.JsonElement? _lastCronStatus;

    public void UpdateCronList(System.Text.Json.JsonElement data)
    {
        _lastCronList = data.Clone();
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is CronPage cp) cp.UpdateFromGateway(data);
            });
        }
        catch { }
    }

    public void UpdateCronStatus(System.Text.Json.JsonElement data)
    {
        _lastCronStatus = data.Clone();
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is CronPage cp) cp.UpdateFromGateway(data);
            });
        }
        catch { }
    }

    public void UpdateCronRuns(System.Text.Json.JsonElement data)
    {
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is CronPage cp) cp.UpdateCronRuns(data);
            });
        }
        catch { }
    }

    public void SeedCronData(CronPage page)
    {
        if (_lastCronList.HasValue) page.UpdateFromGateway(_lastCronList.Value);
        if (_lastCronStatus.HasValue) page.UpdateFromGateway(_lastCronStatus.Value);
    }

    public void UpdateConfig(System.Text.Json.JsonElement config)
    {
        var snapshot = config.Clone();
        LastConfig = snapshot;
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is ConfigPage cp) cp.UpdateConfig(snapshot);
            else if (ContentFrame?.Content is BindingsPage bp) bp.UpdateConfig(snapshot);
        });
    }

    public void UpdateConfigSchema(System.Text.Json.JsonElement schema)
    {
        var snapshot = schema.Clone();
        LastConfigSchema = snapshot;
        if (IsClosed) return;
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is ConfigPage cp) cp.UpdateConfigSchema(snapshot);
            });
        }
        catch { }
    }

    public void UpdateSkillsStatus(System.Text.Json.JsonElement data)
    {
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is SkillsPage sp) sp.UpdateFromGateway(data);
            });
        }
        catch { }
    }

    public void UpdateAgentsList(System.Text.Json.JsonElement data)
    {
        LastAgentsData = data;
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                // Rebuild nav sidebar agent items
                RebuildAgentNavItems(data);
                if (ContentFrame?.Content is HomePage home) home.UpdateAgentsList(data);
            });
        }
        catch { }
    }

    private void RebuildAgentNavItems(System.Text.Json.JsonElement data)
    {
        if (!data.TryGetProperty("agents", out var agentsEl) ||
            agentsEl.ValueKind != System.Text.Json.JsonValueKind.Array) return;

        AgentsNavItem.MenuItems.Clear();

        foreach (var agent in agentsEl.EnumerateArray())
        {
            var id = agent.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(id)) continue;
            var name = agent.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;

            var agentItem = new NavigationViewItem
            {
                Content = name ?? id,
                Tag = $"agent:{id}",
                Icon = new FontIcon { Glyph = "\uE99A" }
            };

            AgentsNavItem.MenuItems.Add(agentItem);
        }
    }

    /// <summary>Extract agent IDs from cached agents data.</summary>
    public List<string> GetAgentIds()
    {
        var ids = new List<string>();
        if (LastAgentsData.HasValue &&
            LastAgentsData.Value.TryGetProperty("agents", out var agentsEl) &&
            agentsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var agent in agentsEl.EnumerateArray())
            {
                var id = agent.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                if (!string.IsNullOrEmpty(id)) ids.Add(id);
            }
        }
        if (ids.Count == 0) ids.Add("main");
        return ids;
    }

    public void UpdateAgentFilesList(System.Text.Json.JsonElement data)
    {
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is WorkspacePage wp) wp.UpdateAgentFilesList(data);
            });
        }
        catch { }
    }

    public void UpdateAgentFileContent(System.Text.Json.JsonElement data)
    {
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is WorkspacePage wp) wp.UpdateAgentFileContent(data);
            });
        }
        catch { }
    }

    // Agent events ring buffer (max 400, cached centrally)
    // All mutations happen on the UI thread via DispatcherQueue
    private const int MaxAgentEvents = 400;
    private readonly System.Collections.Generic.List<AgentEventInfo> _agentEvents = new();
    public System.Collections.Generic.IReadOnlyList<AgentEventInfo> LastAgentEvents => _agentEvents;

    /// <summary>Called by App to also clear its own agent event cache when Clear is invoked.</summary>
    public Action? ClearAppAgentEventsCache { get; set; }

    public void ClearAgentEvents()
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            _agentEvents.Clear();
            ClearAppAgentEventsCache?.Invoke();
        });
    }

    /// <summary>Seed the hub's agent event cache from App-level cache (deduplicates by RunId+Seq).</summary>
    public void SeedAgentEvents(IReadOnlyList<AgentEventInfo> appCache)
    {
        DispatcherQueue?.TryEnqueue(() =>
        {
            var existingKeys = new System.Collections.Generic.HashSet<(string, int)>(
                _agentEvents.Select(e => (e.RunId, e.Seq)));
            foreach (var evt in appCache)
            {
                if (!existingKeys.Contains((evt.RunId, evt.Seq)))
                {
                    _agentEvents.Add(evt);
                    if (_agentEvents.Count >= MaxAgentEvents) break;
                }
            }
        });
    }

    public void UpdateAgentEvent(AgentEventInfo evt)
    {
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                _agentEvents.Insert(0, evt);
                if (_agentEvents.Count > MaxAgentEvents)
                    _agentEvents.RemoveRange(MaxAgentEvents, _agentEvents.Count - MaxAgentEvents);
                if (ContentFrame?.Content is AgentEventsPage agentEvents) agentEvents.AddEvent(evt);
            });
        }
        catch { }
    }

    // Pairing data
    public PairingListInfo? LastNodePairList { get; private set; }
    public DevicePairingListInfo? LastDevicePairList { get; private set; }
    public ModelsListInfo? LastModelsList { get; private set; }

    public void UpdateNodePairList(PairingListInfo data)
    {
        LastNodePairList = data;
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is NodesPage np) np.UpdatePairingRequests(data);
            });
        }
        catch { }
    }

    public void UpdateDevicePairList(DevicePairingListInfo data)
    {
        LastDevicePairList = data;
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is NodesPage np) np.UpdateDevicePairingRequests(data);
                if (ContentFrame?.Content is ConnectionPage cp) cp.UpdateDevicePairingRequests(data);
            });
        }
        catch { }
    }

    public void UpdateModelsList(ModelsListInfo data)
    {
        LastModelsList = data;
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is SessionsPage sp) sp.UpdateModelsList(data);
            });
        }
        catch { }
    }

    public PresenceEntry[]? LastPresence { get; private set; }
    public System.Text.Json.JsonElement? LastAgentsData { get; private set; }

    public void UpdatePresence(PresenceEntry[] data)
    {
        LastPresence = data;
        try
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                if (IsClosed) return;
                if (ContentFrame?.Content is InstancesPage ip) ip.UpdatePresenceData(data);
                if (ContentFrame?.Content is NodesPage np) np.UpdatePresence(data);
            });
        }
        catch { }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_suppressNavSelection) return;
        if (args.SelectedItem is not NavigationViewItem item) return;
        if (item.Tag is not string tag || string.IsNullOrEmpty(tag)) return;

        if (tag.StartsWith("agent:", StringComparison.OrdinalIgnoreCase))
        {
            _currentAgentId = ParseAgentIdFromTag(tag);
            _cachedCommands = null;
        }

        var pageType = TagToPageType(tag);
        if (pageType != null)
        {
            ContentFrame.Navigate(pageType);
            InitializeCurrentPage();
        }

        // Variant C-1: any selection collapses the overlay so the user sees
        // the chosen page immediately.
        if (NavView.IsPaneOpen) NavView.IsPaneOpen = false;
    }

    /// <summary>
    /// Variant C-1: pane state is no longer persisted — the overlay always
    /// starts closed. We keep the handler wired so XAML doesn't break, but it
    /// no longer touches <see cref="SettingsManager.HubNavPaneOpen"/>.
    /// </summary>
    private void OnNavPaneStateChanged(NavigationView sender, object args)
    {
        // Intentional no-op for Variant C-1.
    }

    private void InitializeCurrentPage() => InitializePage(ContentFrame.Content);

    internal void InitializePage(object? content)
    {
        switch (content)
        {
            case HomePage home: home.Initialize(this); break;
            case ChatPage chat: chat.Initialize(this); break;
            case SessionsPage sessions:
                sessions.Initialize(this);
                if (LastModelsList != null) sessions.UpdateModelsList(LastModelsList);
                break;
            case ConnectionPage connection:
                connection.Initialize(this);
                if (LastDevicePairList != null) connection.UpdateDevicePairingRequests(LastDevicePairList);
                break;
            case ChannelsPage channels: channels.Initialize(this); break;
            case UsagePage usage: usage.Initialize(this); break;
            case NodesPage nodes:
                nodes.Initialize(this);
                if (LastNodePairList != null) nodes.UpdatePairingRequests(LastNodePairList);
                if (LastDevicePairList != null) nodes.UpdateDevicePairingRequests(LastDevicePairList);
                if (LastPresence != null) nodes.UpdatePresence(LastPresence);
                break;
            case CronPage cron: cron.Initialize(this); SeedCronData(cron); break;
            case SkillsPage skills: skills.Initialize(this); break;
            case ConfigPage config:
                try
                {
                    config.Initialize(this);
                    if (LastConfigSchema.HasValue) config.UpdateConfigSchema(LastConfigSchema.Value);
                    if (LastConfig.HasValue) config.UpdateConfig(LastConfig.Value);
                }
                catch (Exception ex)
                {
                    OpenClawTray.Services.Logger.Error($"[HubWindow] ConfigPage seed failed: {ex}");
                }
                break;
            case InstancesPage instances:
                instances.Initialize(this);
                if (LastPresence != null) instances.UpdatePresenceData(LastPresence);
                break;
            case PermissionsPage permissions: permissions.Initialize(this); break;
            case CapabilitiesPage capabilities: capabilities.Initialize(this); break;
            case SandboxPage sandbox: sandbox.Initialize(this); break;
            case VoiceSettingsPage voice: voice.Initialize(this, VoiceServiceInstance); break;
            case ConversationsPage convos: convos.Initialize(this); break;
            case ActivityPage activity: activity.Initialize(this); break;
            case AgentEventsPage agentEvents:
                agentEvents.ClearCentralCache = ClearAgentEvents;
                agentEvents.PopulateAgentFilter(this);
                // When navigated via top-level nav (tag "agentevents"), show all agents
                var agentEventsTag = (NavView?.SelectedItem as NavigationViewItem)?.Tag as string;
                var eventsAgentFilter = agentEventsTag?.StartsWith("agent:") == true ? _currentAgentId : null;
                agentEvents.SetAgentFilter(eventsAgentFilter);
                if (agentEvents.EventCount == 0 && LastAgentEvents != null)
                {
                    for (int i = LastAgentEvents.Count - 1; i >= 0; i--)
                        agentEvents.AddEvent(LastAgentEvents[i]);
                }
                break;
            case WorkspacePage workspace: workspace.Initialize(this); break;
            case BindingsPage bindings:
                bindings.Initialize(this);
                if (LastConfig.HasValue) bindings.UpdateConfig(LastConfig.Value);
                break;
            case SettingsPage settings: settings.Initialize(this); break;
            case DebugPage debug: debug.Initialize(this); break;
            case AboutPage about: about.Initialize(this); break;
            case OpenClawTray.Pages.Settings.StatusPage status: status.Initialize(this); break;
        }
    }

    public void SetActivityFilter(string? filter)
    {
        if (IsClosed) return;
        DispatcherQueue?.TryEnqueue(() =>
        {
            if (ContentFrame?.Content is ActivityPage activity)
                activity.SetFilter(filter);
        });
    }

    // Tags that previously routed through SettingsHostPage. Variant C-1 maps
    // them all directly to their feature pages via TagToPageType — no
    // separate host shim. Kept (empty) for any external code still importing it.
    internal static readonly HashSet<string> SettingsHostedTags = new(StringComparer.OrdinalIgnoreCase);

    private static Type? TagToPageType(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;

        // Strip per-node namespace ("thispc:capabilities" → "capabilities")
        // so all node sub-tags reuse the same per-feature pages. Branch tags
        // like "node:thispc" stay intact and fall through to the switch below.
        var key = tag;
        if (!tag.StartsWith("node:", StringComparison.OrdinalIgnoreCase) &&
            !tag.StartsWith("agent:", StringComparison.OrdinalIgnoreCase))
        {
            var colon = tag.IndexOf(':');
            if (colon > 0) key = tag.Substring(colon + 1);
        }

        return key.ToLowerInvariant() switch
        {
            "home" or "chat" or "general" => typeof(ChatPage),
            "status" => typeof(OpenClawTray.Pages.Settings.StatusPage),
            "connection" => typeof(ConnectionPage),
            "channels" => typeof(ChannelsPage),
            "nodes" => typeof(NodesPage),
            "instances" => typeof(InstancesPage),
            "config" => typeof(ConfigPage),
            "usage" => typeof(UsagePage),
            "bindings" => typeof(BindingsPage),
            "capabilities" => typeof(CapabilitiesPage),
            "voice" => typeof(VoiceSettingsPage),
            "permissions" => typeof(PermissionsPage),
            "sandbox" => typeof(SandboxPage),
            "activity" => typeof(ActivityPage),
            "apppreferences" or "settings" => typeof(SettingsPage),
            "debug" => typeof(DebugPage),
            "info" or "about" => typeof(AboutPage),
            "conversations" => typeof(ConversationsPage),
            "sessions" => typeof(SessionsPage),
            "agentevents" => typeof(AgentEventsPage),
            "skills" => typeof(SkillsPage),
            "agents" => typeof(WorkspacePage),
            "cron" => typeof(CronPage),
            "workspace" => typeof(WorkspacePage),
            // Branch parents (no page) — selectable=false in the tree, so
            // we'll never actually navigate here, but return null defensively.
            "gateway" or "diagnostics" => null,
            _ when tag.StartsWith("node:", StringComparison.OrdinalIgnoreCase) => null,
            _ when tag.StartsWith("agent:", StringComparison.OrdinalIgnoreCase) => ResolveAgentPageType(tag),
            _ => null
        };
    }

    private static Type? ResolveAgentPageType(string tag)
    {
        var parts = tag.Split(':');
        // "agent:main" (2 parts) → workspace page for that agent
        if (parts.Length == 2) return typeof(WorkspacePage);
        // "agent:main:workspace" etc (3 parts)
        return parts[2] switch
        {
            "sessions" => typeof(SessionsPage),
            "agentevents" => typeof(AgentEventsPage),
            "skills" => typeof(SkillsPage),
            "cron" => typeof(CronPage),
            "workspace" => typeof(WorkspacePage),
            _ => null
        };
    }

    private static string ParseAgentIdFromTag(string? tag)
    {
        if (tag == null || !tag.StartsWith("agent:")) return "main";
        var parts = tag.Split(':');
        return parts.Length >= 2 ? parts[1] : "main";
    }

    // ── Command Search (Ctrl+E / Ctrl+K / Ctrl+F) — title bar AutoSuggestBox ──

    private void OnRootPreviewKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(
            global::Windows.System.VirtualKey.Control).HasFlag(
            global::Windows.UI.Core.CoreVirtualKeyStates.Down);
        if (ctrl && (e.Key == global::Windows.System.VirtualKey.E ||
                     e.Key == global::Windows.System.VirtualKey.K ||
                     e.Key == global::Windows.System.VirtualKey.F))
        {
            e.Handled = true;
            TitleSearchBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            TitleSearchBox.Text = "";
        }
    }

    private List<CommandItem>? _cachedCommands;

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _cachedCommands ??= BuildCommandList();
        var query = sender.Text?.Trim() ?? "";
        var filtered = string.IsNullOrEmpty(query)
            ? _cachedCommands.Take(8).ToList()
            : _cachedCommands.Where(c => c.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || (c.Subtitle?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                .Take(10).ToList();
        sender.ItemsSource = filtered;
    }

    private void OnSearchSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is CommandItem cmd)
        {
            sender.Text = "";
            sender.ItemsSource = null;
            _cachedCommands = null;
            ExecuteCommand(cmd);
        }
    }

    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is CommandItem cmd)
        {
            sender.Text = "";
            sender.ItemsSource = null;
            _cachedCommands = null;
            ExecuteCommand(cmd);
        }
        else if (sender.ItemsSource is List<CommandItem> items && items.Count > 0)
        {
            // Enter pressed without selecting — execute first match
            var first = items[0];
            sender.Text = "";
            sender.ItemsSource = null;
            _cachedCommands = null;
            ExecuteCommand(first);
        }
    }

    internal List<CommandItem> BuildCommandList()
    {
        var agentId = _currentAgentId;
        var commands = new List<CommandItem>
        {
            // Navigation
            new() { Icon = "🏠", Title = "Go to Home", Subtitle = "Home page", Tag = "home" },
            new() { Icon = "💬", Title = "Go to Chat", Subtitle = "Open chat", Tag = "chat" },
            new() { Icon = "🧠", Title = "Go to Sessions", Subtitle = "All sessions", Tag = "sessions" },
            new() { Icon = "🧠", Title = "Go to Agent Events", Subtitle = "Agent event log", Tag = "agentevents" },
            new() { Icon = "🧠", Title = "Go to Skills", Subtitle = "Registered skills", Tag = "skills" },
            new() { Icon = "🧠", Title = $"Go to Cron ({agentId})", Subtitle = "Scheduled tasks", Tag = $"agent:{agentId}:cron" },
            new() { Icon = "🧠", Title = $"Go to Workspace ({agentId})", Subtitle = "Workspace files", Tag = $"agent:{agentId}" },
            new() { Icon = "📡", Title = "Go to Channels", Subtitle = "Gateway channels", Tag = "channels" },
            new() { Icon = "📡", Title = "Go to Nodes", Subtitle = "Connected nodes", Tag = "nodes" },
            new() { Icon = "📡", Title = "Go to Instances", Subtitle = "Gateway instances", Tag = "instances" },
            new() { Icon = "📡", Title = "Go to Config", Subtitle = "Gateway configuration", Tag = "config" },
            new() { Icon = "📡", Title = "Go to Usage", Subtitle = "Usage statistics", Tag = "usage" },
            new() { Icon = "📡", Title = "Go to Bindings", Subtitle = "Gateway bindings", Tag = "bindings" },
            new() { Icon = "🖥️", Title = "Go to Capabilities", Subtitle = "Device capabilities", Tag = "capabilities" },
            new() { Icon = "🛡️", Title = "Go to Permissions", Subtitle = "Exec policy & allowlists", Tag = "permissions" },
            new() { Icon = "🕐", Title = "Go to Activity", Subtitle = "Activity stream", Tag = "activity" },
            new() { Icon = "⚙️", Title = "Go to Settings", Subtitle = "Application settings", Tag = "settings" },
            new() { Icon = "🐛", Title = "Go to Debug", Subtitle = "Debug information", Tag = "debug" },
            new() { Icon = "ℹ️", Title = "Go to Info", Subtitle = "About this app", Tag = "info" },

            // Actions
            new() { Icon = "💬", Title = "Open Chat Window", Subtitle = "Open standalone chat", Tag = "chat" },
            new() { Icon = "🌐", Title = "Open Dashboard", Subtitle = "Open web dashboard", Execute = () => OpenDashboardAction?.Invoke(null) },
            new() { Icon = "📤", Title = "Quick Send", Subtitle = "Send a quick message", Execute = () => QuickSendAction?.Invoke() },
        };

        // Toggle commands
        if (Settings != null)
        {
            commands.Add(new CommandItem
            {
                Icon = "🔌", Title = "Toggle Node Mode",
                Subtitle = Settings.EnableNodeMode ? "Currently ON" : "Currently OFF",
                Execute = () => { Settings.EnableNodeMode = !Settings.EnableNodeMode; Settings.Save(); RaiseSettingsSaved(); }
            });
            commands.Add(new CommandItem
            {
                Icon = "📷", Title = "Toggle Camera",
                Subtitle = Settings.NodeCameraEnabled ? "Currently ON" : "Currently OFF",
                Execute = () => { Settings.NodeCameraEnabled = !Settings.NodeCameraEnabled; Settings.Save(); RaiseSettingsSaved(); }
            });
            commands.Add(new CommandItem
            {
                Icon = "🎨", Title = "Toggle Canvas",
                Subtitle = Settings.NodeCanvasEnabled ? "Currently ON" : "Currently OFF",
                Execute = () => { Settings.NodeCanvasEnabled = !Settings.NodeCanvasEnabled; Settings.Save(); RaiseSettingsSaved(); }
            });
            commands.Add(new CommandItem
            {
                Icon = "🖥️", Title = "Toggle Screen Capture",
                Subtitle = Settings.NodeScreenEnabled ? "Currently ON" : "Currently OFF",
                Execute = () => { Settings.NodeScreenEnabled = !Settings.NodeScreenEnabled; Settings.Save(); RaiseSettingsSaved(); }
            });
            commands.Add(new CommandItem
            {
                Icon = "🌐", Title = "Toggle Browser Control",
                Subtitle = Settings.NodeBrowserProxyEnabled ? "Currently ON" : "Currently OFF",
                Execute = () => { Settings.NodeBrowserProxyEnabled = !Settings.NodeBrowserProxyEnabled; Settings.Save(); RaiseSettingsSaved(); }
            });
        }

        // Dynamic session commands
        if (LastSessions != null)
        {
            foreach (var session in LastSessions)
            {
                var key = session.Key;
                commands.Add(new CommandItem
                {
                    Icon = "🧠", Title = $"Go to session: {key}",
                    Subtitle = "Open in dashboard",
                    Execute = () => OpenDashboardAction?.Invoke($"sessions/{key}")
                });
            }
        }

        return commands;
    }

    private void ExecuteCommand(CommandItem cmd)
    {
        if (cmd.Execute != null)
        {
            cmd.Execute();
            return;
        }

        if (!string.IsNullOrEmpty(cmd.Tag))
        {
            NavigateTo(cmd.Tag);
        }
    }

    /// <summary>Action to open the QuickSend dialog, set by App.xaml.cs.</summary>
    public Action? QuickSendAction { get; set; }
}
