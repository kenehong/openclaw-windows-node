# Concept: `node-mode`

> Master switch that registers this PC as an OpenClaw node and starts
> advertising the enabled capabilities to agents over the gateway.
> When off, no capability runs regardless of individual toggles.

## Canonical name

`Node mode`. Sentence-case. Don't say "Node toggle", "Server mode",
"Headless mode".

## Copy

- **Card title**: `Node mode`
- **Card description** (under toggle):
  > When on, this PC registers as a node and offers the capabilities
  > below to agents over the gateway.
- **Status sub-row labels** (live):
  - off → `Node mode disabled` + empty details.
  - on, providing capabilities → `Node active` + details
    `Providing N capabilities: …` (canonical comma list — see
    `naming.md` → Status vocabulary).
  - on, no capabilities → `Node active` + details
    `Providing no capabilities`.
  - on, error / not connected → status text follows Gateway concept
    (TBD: `concepts/states/gateway-connection.md`).

## Icon

- Concept / metaphor: **This PC** (a desktop PC offering itself as a
  node).
- Segoe Fluent: `PC1` (U+E839) on Permissions card header — see
  [CDR-0001](../_decisions/0001-node-mode-glyph.md); status sub-row
  uses a colored `Ellipse` (12 px) instead of a glyph.
- `FluentIconCatalog` const: `System`.
- Status dot color (sub-row):
  - off → `SystemFillColorNeutralBrush`.
  - active → `SystemFillColorSuccessBrush`.
  - error / waiting → `SystemFillColorCautionBrush`.

## States

| State                  | Master toggle | Status dot | Status text             | Capability list |
|------------------------|:------------:|:----------:|-------------------------|-----------------|
| disabled               | off          | neutral    | `Node mode disabled`    | dimmed to 40 %, toggles frozen |
| enabled, idle          | on           | success    | `Node active`           | full opacity   |
| enabled, error         | on           | critical   | (gateway error text)    | full opacity, but capabilities not actually served |

## Appears in

- `surfaces/permissions-page.md` — node status card (top of page).
- Tray flyout header dot + the tray Permissions flyout master switch.
- Onboarding wizard "Turn on node mode" step.

## Code anchors

- Setting flag: `Settings.EnableNodeMode` (bool).
- XAML: `Pages/PermissionsPage.xaml` — `NodeStatusCard`
  (`tk:SettingsExpander`), `NodeModeToggle`, `NodeStatusDot`,
  `NodeStatusText`, `NodeDetailsText`.
- Code-behind: `Pages/PermissionsPage.xaml.cs` — `OnNodeModeToggled`,
  `UpdateNodeStatus`.
- Capability list builder (status details):
  `App.xaml.cs` (`caps.Add("browser")` etc.).
- Tray flyout master switch: in `App.xaml.cs` Permissions flyout
  builder (search `"Permissions"` header).

## Do / Don't

- ✅ Always reflect Node mode in the status sub-row immediately on
  toggle — never wait for a network round trip to update the local
  visual state.
- ✅ Dim individual capability toggles when Node mode is off (opacity
  0.4) instead of hiding them, so users still see what they can opt
  into.
- ❌ Don't show a green dot just because the toggle is on — the dot
  must reflect actual `Node active` (connected + advertising). When
  the gateway is unreachable the dot is critical, not success.
