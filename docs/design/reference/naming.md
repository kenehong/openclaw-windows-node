# Naming — Product Vocabulary

Canonical English words used across every OpenClaw Windows-node
surface. Pick the term on the left; the terms on the right are
prohibited (or have a different meaning — read the note).

| Canonical            | Don't use                                  | Notes |
|----------------------|--------------------------------------------|-------|
| Node                 | Device, Endpoint, Client                   | "node" = this PC offering capabilities to agents via the gateway. "client" is reserved for things consuming the gateway (see Sessions). |
| Node mode            | Node toggle, Headless mode                 | The master switch in Permissions / tray. |
| Gateway              | Server, Broker, Hub                        | The relay this node is registered against. "Hub" is the internal in-app window name only. |
| Operator             | User, Owner, Admin                         | The human (or operator-token-bearing app) that drives the agents. |
| Agent                | Model, Bot, Assistant                      | Whatever calls capabilities through the gateway. |
| Capability           | Permission, Feature, Tool                  | A device-side thing the node can do (browser, camera, canvas, …). "Permission" is the page name; capabilities are the rows on it. |
| Permissions          | Settings, Privacy                          | The page that toggles capabilities. Plural. |
| Companion Settings   | Settings, Preferences, Options             | The full in-app settings window — distinguishes from the tray's quick menu. Always followed by `…` (opens a window). |
| Paired               | Connected, Linked, Authorized              | A device has finished bootstrap and holds its own device token. |
| Connected            | Online, Up                                 | Live WebSocket / control channel is open. Orthogonal to paired. |
| Disconnected         | Offline, Down                              | Live channel is closed. |
| Unpaired             | New, Unauthorized, Pending                 | No device token yet. |
| Quick Send           | Share, Push, Send                          | The send-from-PC quick action. Always followed by `…`. |
| Reconfigure          | Re-pair, Switch gateway, Reset             | Action that walks the user back through the onboarding wizard. Always followed by `…`. |
| Canvas               | Whiteboard, Drawing, Shared view           | Product term; do not translate or paraphrase. |
| Browser control      | Browser, Web                               | Full capability label. Short form "browser" is allowed only inside `status` strings (e.g. `Providing N capabilities: …, browser, …`). |
| Screen capture       | Screenshot, Screen share, Display          | Full label on Permissions page. Short form "screen". |
| Location             | GPS, Geolocation                           | Short label. Description must say "approximate". |
| Text-to-speech       | TTS, Speech output, Read aloud             | Hyphenated, lowercase "to". Short form "tts" only inside config keys / status. |
| Speech-to-text       | STT, Dictation, Transcription              | Hyphenated, lowercase "to". Short form "stt" only inside config keys / status. |

## Status vocabulary (short forms used in status lines)

Exactly these strings, lowercase, comma-separated where appropriate:

- `connected` · `connecting` · `disconnected` · `reconnecting`
- `unpaired` · `node paired` · `operator paired`
- `Providing N capabilities: browser, camera, canvas, screen, location, tts, stt`
  - Order is fixed: matches the Permissions page order.
  - Empty list renders as `Providing no capabilities`.

## Typographic conventions

- **Ellipsis `…`** — appended to a label only when activation opens a
  modal, window, or wizard. Examples: `Quick Send…`, `Reconfigure…`,
  `Companion Settings…`, `More voice settings…`. Plain actions
  (Dashboard, Chat, About) take no ellipsis.
- **Sentence case** for menu items, labels, and one-line descriptions.
  Title Case is reserved for page titles (`Permissions`, `Companion
  Settings`).
- **Em dash `—`** in descriptions to attach an example clause:
  `… on this PC — open pages, click, and read content.`
- **No trailing periods** in row labels. Descriptions are full
  sentences and end with a period.
