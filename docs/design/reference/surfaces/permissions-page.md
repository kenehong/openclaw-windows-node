# Surface: Permissions Page (`PermissionsPage.xaml`)

In-app page inside the Companion Settings window. Lists every node
capability with a toggle. Page padding 24 px, `MaxWidth=900`,
inter-card spacing 8 px (`Spacing="8"` on the outer `StackPanel`).
The page is composed of CommunityToolkit `SettingsCard` /
`SettingsExpander` controls — do not hand-roll `Border + Grid` rows.

Composition only — labels, icons, copy come from the concept files.

## Required package

`CommunityToolkit.WinUI.Controls.SettingsControls` (≥ 8.2.251219).
Declared in `OpenClaw.Tray.WinUI.csproj`. Namespace prefix used in
XAML:

```xml
xmlns:tk="using:CommunityToolkit.WinUI.Controls"
```

## Layout (top to bottom)

1. **Page header**
   - Title `Permissions` (`TitleTextBlockStyle`).
   - Hostname line: `Devices` glyph (U+E977, 14 px,
     `TextFillColorSecondaryBrush`) + hostname text
     (`CaptionTextBlockStyle`, secondary).
   - Intro caption (resw `PermissionsPage_IntroText`).
2. **Node mode** — `concepts/states/node-mode.md`. Implemented as a
   `tk:SettingsExpander`:
   - `HeaderIcon`: `FontIcon` with glyph U+E839 (`PC1`,
     `FluentIconCatalog.System`) — see [CDR-0001](../concepts/_decisions/0001-node-mode-glyph.md).
   - `Header`: `"Node mode"` (string).
   - `Description`: one-line copy from the concept file.
   - `Content` (right side): `NodeModeToggle` `ToggleSwitch`.
   - `IsExpanded` is driven by the Node-mode-enabled state so the
     status sub-row only appears when relevant.
   - Inside `tk:SettingsExpander.Items`, a single `tk:SettingsCard`
     for the status sub-row:
     - `Header` is a horizontal `StackPanel` holding a 10 px
       `NodeStatusDot` `Ellipse` and the `NodeStatusText` label.
     - `Description` is `NodeDetailsText`
       (`Providing N capabilities: …`).
3. **Capabilities section header** — `BodyStrongTextBlockStyle`
   "Capabilities" + caption description.
4. **Capability rows** (built programmatically by
   `BuildCapabilityToggles` → `BuildCapabilityRow`, in this fixed
   order). Each row is a `tk:SettingsCard`:
   1. Browser control — `concepts/capabilities/browser-control.md` *(TBD)*
   2. Camera — `concepts/capabilities/camera.md`
   3. Canvas — `concepts/capabilities/canvas.md`
   4. Screen capture — *(TBD)*
   5. Location — *(TBD)*
   6. Text-to-speech — *(TBD)*
   7. Speech-to-text — *(TBD)*
5. **STT details card** — visible only when STT is enabled. Subtle
   secondary fill, info glyph (U+E946, 14 px), link to "More voice
   settings…". *(still a custom `Border`; convert when the STT
   concept doc lands.)*
6. **TTS details card** *(TBD — drives provider choice; still a
   custom `Border`).*

## Row anatomy (capability row → `tk:SettingsCard`)

| `SettingsCard` slot | Source                                         |
|---------------------|------------------------------------------------|
| `HeaderIcon`        | `FontIcon` with `FluentIconCatalog.<Concept>`  |
| `Header`            | Resw label (`PermissionsPage_Cap_*_Label`)     |
| `Description`       | Resw description (`PermissionsPage_Cap_*_Desc`)|
| `Content`           | `ToggleSwitch` for the feature                 |

The toolkit applies the standard Win 11 Settings padding, divider,
rounded corners, and disabled state — do not override these. Spacing
between sibling `SettingsCard`s is 8 px from the parent `StackPanel`.

## Dim / disabled behavior

When `Node mode` is off, the entire `CapabilityRepeater` drops to
opacity 0.4 (`UpdateNodeStatus` in `PermissionsPage.xaml.cs`).
Individual toggles remain technically interactive but should not be —
fix tracked separately.

## Reference screenshots

`docs/design/assets/permissions-page-{light,dark,hc}.png` (TBD).

## Code anchors

- XAML: `src/OpenClaw.Tray.WinUI/Pages/PermissionsPage.xaml`
- Code-behind: `src/OpenClaw.Tray.WinUI/Pages/PermissionsPage.xaml.cs`
- Capability tuple list & row builder: `BuildCapabilityToggles` /
  `BuildCapabilityRow`.
- Resw keys: `PermissionsPage_*` in
  `src/OpenClaw.Tray.WinUI/Strings/<locale>/Resources.resw`.

## Do / Don't

- ✅ Add a new capability by (1) writing a concept file, (2) adding
  the tuple in `BuildCapabilityToggles`, (3) adding resw entries in
  all locales, (4) updating `iconography.md` and
  `surfaces-index.md`.
- ✅ Use `tk:SettingsCard` / `tk:SettingsExpander` for any new row on
  this page — match the Win 11 Settings chrome.
- ❌ Don't put a `Shape` (e.g. `Ellipse`) directly into a
  `HeaderIcon` slot — it must be an `IconElement`. Place the dot
  inside `Header` content instead.
- ❌ Don't render an emoji as the row icon — use the Segoe Fluent
  glyph from `FluentIconCatalog`.
- ❌ Don't reorder capabilities — the order matches the canonical
  status-line order in `naming.md`.
