# Reference: CommunityToolkit `SettingsCard` / `SettingsExpander`

## What it is

Two controls in the **Windows Community Toolkit** that implement the
Win 11 Settings row chrome (rounded card, icon + header + description
+ content slot) so consumers don't have to hand-roll `Border + Grid`.

- `SettingsCard` — single row.
- `SettingsExpander` — same row, expandable to reveal a list of
  child `SettingsCard` items.

## Why we care

This is the control we standardize on for **every app-page settings
surface** in the OpenClaw Windows node. Without it, each page would
re-implement the Win 11 Settings visual idiom by hand and the
surfaces would drift visually within one release.

## Canonical links

- **`SettingsCard` documentation** —
  https://learn.microsoft.com/en-us/dotnet/communitytoolkit/windows/controls/settingscard
- **`SettingsExpander` documentation** —
  https://learn.microsoft.com/en-us/dotnet/communitytoolkit/windows/controls/settingsexpander
- **Source repository** —
  https://github.com/CommunityToolkit/Windows
  - Project: `components/SettingsControls/`.
- **NuGet package** —
  https://www.nuget.org/packages/CommunityToolkit.WinUI.Controls.SettingsControls/

## Package version pinned

`CommunityToolkit.WinUI.Controls.SettingsControls` **8.2.251219**
(see `src/OpenClaw.Tray.WinUI/OpenClaw.Tray.WinUI.csproj`). Requires
`Microsoft.WindowsAppSDK` ≥ 1.6.250108002 — we ship 2.0.1, so compatible.

## Slot reference

XAML namespace prefix used in OpenClaw:

```xml
xmlns:tk="using:CommunityToolkit.WinUI.Controls"
```

### `tk:SettingsCard`

| Slot          | Type           | OpenClaw usage                                    |
|---------------|----------------|---------------------------------------------------|
| `HeaderIcon`  | `IconElement`  | `FontIcon` with `FluentIconCatalog.<Concept>` glyph |
| `Header`      | `object` / `string` | Resw label (e.g. `PermissionsPage_Cap_Camera_Label`) |
| `Description` | `object` / `string` | Resw description (e.g. `PermissionsPage_Cap_Camera_Desc`) |
| `Content`     | `object`       | Right-side control: `ToggleSwitch`, `ComboBox`, `HyperlinkButton`, …  |

### `tk:SettingsExpander`

| Slot          | Type           | OpenClaw usage                                    |
|---------------|----------------|---------------------------------------------------|
| `HeaderIcon`  | `IconElement`  | `FontIcon` with the master concept's glyph        |
| `Header`      | `object` / `string` | Concept label                                |
| `Description` | `object` / `string` | One-liner                                    |
| `Content`     | `object`       | Master control (e.g. `NodeModeToggle` `ToggleSwitch`) |
| `Items`       | `IList`        | Child rows — each a `tk:SettingsCard`             |
| `IsExpanded`  | `bool`         | Bind to a relevant state (e.g. Node-mode enabled) |

## Gotchas (learned the hard way)

- **`HeaderIcon` requires `IconElement`**, not `Shape`. Putting a
  bare `Ellipse` or `Rectangle` into a `HeaderIcon` will fail XAML
  compile **silently** (`XamlCompiler.exe` exits 1 with no error
  text). Place a status dot inside the `Header` content instead.
- **`Header` / `Description` accept plain strings**; only wrap them
  in a `TextBlock` when you need `TextWrapping="Wrap"` or specific
  styling.
- **`Content` is right-aligned by default**, and the toolkit applies
  Win 11 Settings padding / divider / rounded corners — do not
  override `Padding` / `CornerRadius` on a `SettingsCard`.
- Spacing between sibling `SettingsCard`s comes from the parent
  `StackPanel.Spacing` (we use `8`), not from card margins.

## Local mapping

| Slot rule / gotcha               | OpenClaw doc                                  |
|----------------------------------|-----------------------------------------------|
| Slot table & required types      | `docs/design/reference/iconography.md` (icon sizes), `docs/design/reference/surfaces/permissions-page.md` (row anatomy table) |
| `HeaderIcon` IconElement gotcha  | `docs/design/reference/iconography.md` Rendering rules  |
| Hand-rolling Border ban          | `docs/design/SKILL.md` Hard rules |
