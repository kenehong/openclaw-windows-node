using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using OpenClaw.Shared;
using OpenClawTray.Services;
using OpenClawTray.Windows;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace OpenClawTray.Pages;

public sealed partial class ChatPage : Page
{
    private HubWindow? _hub;
    private string _chatUrl = "";
    private bool _webViewInitialized;
    private global::Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2NavigationCompletedEventArgs>? _navCompletedHandler;
    private global::Windows.Foundation.TypedEventHandler<CoreWebView2, CoreWebView2NavigationStartingEventArgs>? _navStartingHandler;

    public ChatPage()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
        Shell.SendRequested += OnShellSendRequested;
        Shell.DropdownChanged += OnShellDropdownChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2 != null)
        {
            if (_navCompletedHandler != null)
                WebView.CoreWebView2.NavigationCompleted -= _navCompletedHandler;
            if (_navStartingHandler != null)
                WebView.CoreWebView2.NavigationStarting -= _navStartingHandler;
        }
    }

    public void Initialize(HubWindow hub)
    {
        _hub = hub;
        if (!_webViewInitialized && hub.Settings != null)
        {
            _ = InitializeWebViewAsync(hub.Settings);
        }
    }

    private async Task InitializeWebViewAsync(SettingsManager settings)
    {
        try
        {
            var gatewayUrl = settings.GetEffectiveGatewayUrl();
            if (string.IsNullOrEmpty(gatewayUrl))
            {
                return;
            }

            if (!TryBuildChatUrl(gatewayUrl, settings.Token, out var chatUrl, out var errorMessage))
            {
                PlaceholderPanel.Visibility = Visibility.Collapsed;
                ErrorPanel.Visibility = Visibility.Visible;
                ErrorText.Text = errorMessage;
                return;
            }

            _chatUrl = chatUrl;

            PlaceholderPanel.Visibility = Visibility.Collapsed;
            LoadingRing.IsActive = true;
            LoadingRing.Visibility = Visibility.Visible;

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenClawTray", "WebView2");
            Directory.CreateDirectory(userDataFolder);
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);

            await WebView.EnsureCoreWebView2Async();
            _webViewInitialized = true;

            WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
            WebView.CoreWebView2.Settings.IsZoomControlEnabled = true;

            // Inject CSS hide on every document creation. This runs before any page script,
            // so the style is present before React/Lit components render.
            await WebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (function() {
                    function inject() {
                        if (!document.head) return false;
                        if (document.head.querySelector('style[data-openclaw-hide]')) return true;
                        var style = document.createElement('style');
                        style.setAttribute('data-openclaw-hide', 'true');
                        style.textContent = `
                            html, body { height: 100% !important; width: 100% !important; margin: 0 !important; padding: 0 !important; overflow: hidden !important; }
                            header.topbar, .shell-nav, aside.sidebar, .content--chat > .content-header, .agent-chat__input { display: none !important; }
                            .shell, .shell.shell--chat { display: flex !important; flex-direction: column !important; height: 100vh !important; width: 100vw !important; grid-template-columns: none !important; grid-template-rows: none !important; grid-template-areas: none !important; }
                            main.content { flex: 1 1 auto !important; min-height: 0 !important; height: auto !important; width: 100% !important; max-width: none !important; margin: 0 !important; padding: 0 !important; display: flex !important; flex-direction: column !important; grid-area: auto !important; }
                            .content--chat { flex: 1 1 auto !important; min-height: 0 !important; height: auto !important; width: 100% !important; max-width: none !important; display: flex !important; flex-direction: column !important; }
                            .agent-chat, .agent-chat__main, .agent-chat__thread, .chat-list, .chat-messages { flex: 1 1 auto !important; min-height: 0 !important; }
                        `;
                        document.head.appendChild(style);
                        return true;
                    }
                    if (!inject()) {
                        var iv = setInterval(function(){ if (inject()) clearInterval(iv); }, 50);
                    }
                })();
            ");

            _navCompletedHandler = (s, e) =>
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;

                if (e.IsSuccess)
                {
                    _ = PopulateDropdownsAsync();
                }
                else if (e.WebErrorStatus == CoreWebView2WebErrorStatus.ConnectionAborted ||
                                      e.WebErrorStatus == CoreWebView2WebErrorStatus.CannotConnect ||
                                      e.WebErrorStatus == CoreWebView2WebErrorStatus.ConnectionReset ||
                                      e.WebErrorStatus == CoreWebView2WebErrorStatus.ServerUnreachable)
                {
                    WebView.Visibility = Visibility.Collapsed;
                    ErrorPanel.Visibility = Visibility.Visible;
                    ErrorText.Text = $"Cannot connect to gateway at {gatewayUrl}\n\nMake sure the gateway is running.";
                }
            };
            WebView.CoreWebView2.NavigationCompleted += _navCompletedHandler;

            _navStartingHandler = (s, e) =>
            {
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
            };
            WebView.CoreWebView2.NavigationStarting += _navStartingHandler;

            WebView.Visibility = Visibility.Visible;
            WebView.CoreWebView2.Navigate(_chatUrl);
        }
        catch (Exception ex)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            PlaceholderPanel.Visibility = Visibility.Collapsed;
            WebView.Visibility = Visibility.Collapsed;
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorText.Text = $"WebView2 failed to initialize:\n{ex.Message}";
        }
    }

    private static bool TryBuildChatUrl(string gatewayUrl, string token, out string url, out string errorMessage)
    {
        url = string.Empty;
        errorMessage = string.Empty;

        if (!GatewayUrlHelper.TryNormalizeWebSocketUrl(gatewayUrl, out var normalizedUrl) ||
            !Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var gatewayUri))
        {
            errorMessage = $"Invalid gateway URL: {gatewayUrl}";
            return false;
        }

        var scheme = gatewayUri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase) ? "https" : "http";
        var builder = new UriBuilder(gatewayUri) { Scheme = scheme, Port = gatewayUri.Port };
        var baseUrl = builder.Uri.GetLeftPart(UriPartial.Authority);
        url = $"{baseUrl}?token={Uri.EscapeDataString(token)}";
        return true;
    }

    private void OnHome(object sender, RoutedEventArgs e)
    {
        if (_webViewInitialized && !string.IsNullOrEmpty(_chatUrl))
            WebView.CoreWebView2?.Navigate(_chatUrl);
    }

    private void OnRefresh(object sender, RoutedEventArgs e)
    {
        if (_webViewInitialized)
            WebView.CoreWebView2?.Reload();
    }

    private void OnPopout(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_chatUrl))
        {
            try { Process.Start(new ProcessStartInfo(_chatUrl) { UseShellExecute = true }); }
            catch { }
        }
    }

    private void OnDevTools(object sender, RoutedEventArgs e)
    {
        if (_webViewInitialized)
            WebView.CoreWebView2?.OpenDevToolsWindow();
    }

    private async void OnShellSendRequested(object? sender, string text)
    {
        if (WebView?.CoreWebView2 == null) return;
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            var encoded = System.Text.Json.JsonSerializer.Serialize(text);
            var script = @"
                (function(text){
                    function findTextarea(){
                        return document.querySelector('textarea[placeholder*=""Message Assistant"" i]')
                            || document.querySelector('textarea[placeholder*=Message i]')
                            || document.querySelector('textarea[placeholder*=메시지]')
                            || document.querySelector('.agent-chat__input textarea')
                            || document.querySelector('form textarea')
                            || document.querySelector('textarea');
                    }
                    function findSendButton(form){
                        var root = form || document;
                        return root.querySelector('.chat-send-btn')
                            || root.querySelector('button[aria-label=""Send message""]')
                            || root.querySelector('.agent-chat__send')
                            || root.querySelector('[data-chat-send]')
                            || root.querySelector('button[aria-label*=Send i]')
                            || root.querySelector('button[title*=Send i]')
                            || root.querySelector('button[type=submit]');
                    }
                    function setReactValue(el, value){
                        var proto = el.tagName === 'TEXTAREA' ? window.HTMLTextAreaElement.prototype : window.HTMLInputElement.prototype;
                        var desc = Object.getOwnPropertyDescriptor(proto, 'value');
                        if (desc && desc.set) { desc.set.call(el, value); }
                        else { el.value = value; }
                        el.dispatchEvent(new Event('input', { bubbles: true }));
                        el.dispatchEvent(new Event('change', { bubbles: true }));
                    }
                    var ta = findTextarea();
                    if (!ta) { console.warn('[OpenClaw] no textarea found'); return JSON.stringify({ ok:false, reason:'no-textarea' }); }
                    ta.focus();
                    setReactValue(ta, text);
                    var form = ta.closest('form');
                    var btn = findSendButton(null) || findSendButton(form);
                    if (btn && !btn.disabled) {
                        btn.click();
                        return JSON.stringify({ ok:true, via:'button', btnCls:btn.className });
                    }
                    if (form && typeof form.requestSubmit === 'function') {
                        try { form.requestSubmit(); return JSON.stringify({ ok:true, via:'form-requestSubmit' }); } catch(e){}
                    }
                    if (form) {
                        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
                        return JSON.stringify({ ok:true, via:'form-submit-event' });
                    }
                    var ev = new KeyboardEvent('keydown', { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true });
                    ta.dispatchEvent(ev);
                    return JSON.stringify({ ok:true, via:'enter-key' });
                })(" + encoded + @");
            ";
            var result = await WebView.CoreWebView2.ExecuteScriptAsync(script);
            Debug.WriteLine($"[ChatPage] SendRequested result: {result}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatPage] SendRequested failed: {ex.Message}");
        }
    }

    private async void OnShellDropdownChanged(object? sender, (string Kind, string Value) e)
    {
        if (WebView?.CoreWebView2 == null) return;
        try
        {
            string selector = e.Kind switch
            {
                "channel" => "[data-chat-channel-select], .chat-controls__session-row select:nth-of-type(1)",
                "model" => "[data-chat-model-select], .chat-controls__session-row select:nth-of-type(2)",
                "reasoning" => "[data-chat-reasoning-select], .chat-controls__session-row select:nth-of-type(3)",
                _ => string.Empty,
            };
            if (string.IsNullOrEmpty(selector)) return;

            var encodedSel = System.Text.Json.JsonSerializer.Serialize(selector);
            var encodedVal = System.Text.Json.JsonSerializer.Serialize(e.Value);
            var script = @"
                (function(sel, val){
                    var s = document.querySelector(sel);
                    if (!s) return { ok:false, reason:'no-select' };
                    var matched = false;
                    for (var i = 0; i < s.options.length; i++) {
                        var o = s.options[i];
                        if (o.value === val || (o.textContent || '').trim() === val) { s.selectedIndex = i; matched = true; break; }
                    }
                    if (!matched) s.value = val;
                    s.dispatchEvent(new Event('change', { bubbles: true }));
                    s.dispatchEvent(new Event('input', { bubbles: true }));
                    return { ok:true, matched:matched };
                })(" + encodedSel + ", " + encodedVal + @");
            ";
            await WebView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ChatPage] Dropdown {e.Kind} failed: {ex.Message}");
        }
    }

    private async Task PopulateDropdownsAsync()
    {
        if (WebView?.CoreWebView2 == null) return;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(500);
            try
            {
                var json = await WebView.CoreWebView2.ExecuteScriptAsync(@"
                    (function(){
                        function read(sel){
                            var s = document.querySelector(sel);
                            if (!s) return null;
                            var opts = [];
                            for (var i = 0; i < s.options.length; i++) {
                                var o = s.options[i];
                                opts.push({ value: o.value, label: (o.textContent || '').trim() });
                            }
                            return { value: s.value, options: opts };
                        }
                        return JSON.stringify({
                            channel: read('[data-chat-channel-select], .chat-controls__session-row select:nth-of-type(1)'),
                            model: read('[data-chat-model-select], .chat-controls__session-row select:nth-of-type(2)'),
                            reasoning: read('[data-chat-reasoning-select], .chat-controls__session-row select:nth-of-type(3)')
                        });
                    })();
                ");
                if (string.IsNullOrEmpty(json) || json == "null") continue;
                var inner = System.Text.Json.JsonSerializer.Deserialize<string>(json);
                if (string.IsNullOrEmpty(inner)) continue;
                using var doc = System.Text.Json.JsonDocument.Parse(inner);
                var root = doc.RootElement;

                List<string>? channelOpts = null; string? channelVal = null;
                List<string>? modelOpts = null; string? modelVal = null;
                List<string>? reasoningOpts = null; string? reasoningVal = null;

                if (root.TryGetProperty("channel", out var ch) && ch.ValueKind == System.Text.Json.JsonValueKind.Object)
                    Extract(ch, out channelOpts, out channelVal);
                if (root.TryGetProperty("model", out var md) && md.ValueKind == System.Text.Json.JsonValueKind.Object)
                    Extract(md, out modelOpts, out modelVal);
                if (root.TryGetProperty("reasoning", out var rs) && rs.ValueKind == System.Text.Json.JsonValueKind.Object)
                    Extract(rs, out reasoningOpts, out reasoningVal);

                if (channelOpts != null || modelOpts != null || reasoningOpts != null)
                {
                    Shell.SetDropdownState(channelOpts, channelVal, modelOpts, modelVal, reasoningOpts, reasoningVal);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatPage] PopulateDropdowns attempt {attempt} failed: {ex.Message}");
            }
        }
    }

    private static void Extract(System.Text.Json.JsonElement el, out List<string>? options, out string? value)
    {
        options = null; value = null;
        if (el.TryGetProperty("options", out var optsEl) && optsEl.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            var list = new List<string>();
            foreach (var o in optsEl.EnumerateArray())
            {
                if (o.TryGetProperty("label", out var lab) && lab.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var s = lab.GetString();
                    if (!string.IsNullOrEmpty(s)) list.Add(s!);
                }
            }
            if (list.Count > 0) options = list;
        }
        if (el.TryGetProperty("value", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String)
            value = v.GetString();
    }
}
