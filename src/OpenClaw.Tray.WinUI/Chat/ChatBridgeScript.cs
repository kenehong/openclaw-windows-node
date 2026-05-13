namespace OpenClawTray.Chat;

/// <summary>
/// Variant C-1m: JavaScript injected into the gateway-served chat WebView2
/// (via <c>AddScriptToExecuteOnDocumentCreatedAsync</c>) that watches the
/// transcript DOM for changes and posts the latest user-authored message
/// back to the host through <c>window.chrome.webview.postMessage</c>.
///
/// Heuristic selectors are tried in order; the script is best-effort and
/// fails silently if none match. The host surface should keep a placeholder
/// ("Resume conversation") in that case.
/// </summary>
public static class ChatBridgeScript
{
    public const string MessageType = "lastUserMessage";

    public static readonly string JsSource = @"
(function () {
  if (window.__openClawChatBridge) return;
  window.__openClawChatBridge = true;

  var SELECTORS = [
    '[data-role=""user""]',
    '[data-author=""user""]',
    '[data-message-role=""user""]',
    '[data-from=""user""]',
    '.message.user',
    '.user-message',
    '.chat-message[data-from=""user""]',
    '[class*=""UserMessage""]',
    '[class*=""user-message""]'
  ];

  function findLastUserMessage() {
    for (var i = 0; i < SELECTORS.length; i++) {
      try {
        var nodes = document.querySelectorAll(SELECTORS[i]);
        if (nodes && nodes.length > 0) {
          var last = nodes[nodes.length - 1];
          var text = (last.innerText || last.textContent || '').trim();
          if (text) return text;
        }
      } catch (e) {}
    }
    return null;
  }

  var lastSent = null;
  function publish() {
    try {
      var t = findLastUserMessage();
      if (!t) return;
      if (t.length > 120) t = t.substring(0, 119) + '…';
      if (t === lastSent) return;
      lastSent = t;
      window.chrome.webview.postMessage({ type: '" + MessageType + @"', text: t });
    } catch (e) {}
  }

  function start() {
    publish();
    try {
      var obs = new MutationObserver(function () { publish(); });
      obs.observe(document.body, { childList: true, subtree: true, characterData: true });
    } catch (e) {}
    // Belt-and-suspenders: also poll occasionally for SPAs that mutate via canvas/shadow DOM.
    setInterval(publish, 1500);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start, { once: true });
  } else {
    start();
  }
})();
";
}
