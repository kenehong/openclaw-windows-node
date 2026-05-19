# Iconography — Master Catalog

Every concept that appears in an OpenClaw Windows surface has **one**
canonical icon metaphor, rendered as a Segoe Fluent Icons glyph (PUA
codepoint). Segoe Fluent is the only render target for our UI — the
emoji column below is documentation reference, not a render target.

The single source of truth in code is
[`FluentIconCatalog.cs`](../../../src/OpenClaw.Tray.WinUI/Helpers/FluentIconCatalog.cs).
This table mirrors that file; if you add a row here, add the matching
constant there, and vice versa. `FluentIconCatalogTests` enforces that
the constants stay non-empty and well-formed.

## Rendering policy

| Surface                          | How icons render                               |
|----------------------------------|------------------------------------------------|
| Tray flyouts / right-click menu  | `FontIcon` with `FluentIconCatalog` glyph, 16 px |
| App pages (`SettingsCard` etc.)  | `FontIcon` via `SettingsCard.HeaderIcon`, 20 px  |
| Page header decorations          | `FontIcon`, 16 px                                |

Never render an emoji in a WinUI surface — always use `FontIcon` with
the catalog glyph. The emoji column exists only so docs, log lines,
and informal references (commit messages, chat) can refer to the
**same metaphor** without rendering a glyph.

The single source of truth in code is
[`FluentIconCatalog.cs`](../../../src/OpenClaw.Tray.WinUI/Helpers/FluentIconCatalog.cs).
This table mirrors that file; if you add a row here, add the matching
constant there, and vice versa. `FluentIconCatalogTests` enforces that
the constants stay non-empty and well-formed.

## Capabilities

| Concept          | Metaphor          | Segoe Fluent | Glyph | `FluentIconCatalog` const | Emoji | Resw key (label)                                       |
|------------------|-------------------|--------------|-------|---------------------------|-------|--------------------------------------------------------|
| Browser control  | Globe             | Globe        | E774  | `Browser`                 | 🌐    | `PermissionsPage_Cap_Browser_Label`                    |
| Camera           | Camera            | Camera       | E722  | `Camera`                  | 📷    | `PermissionsPage_Cap_Camera_Label`                     |
| Canvas           | Artist palette    | Color        | E790  | `Canvas` / `CanvasAct`    | 🎨    | `PermissionsPage_Cap_Canvas_Label`                     |
| Screen capture   | Display + clock   | ScreenTime   | EB91  | `Screen`                  | 🖥️    | `PermissionsPage_Cap_Screen_Label`                     |
| Location         | Map pin           | MapPin       | E707  | `Location`                | 📍    | `PermissionsPage_Cap_Location_Label`                   |
| Text-to-speech   | Speaker           | Volume       | E767  | `Voice`                   | 🔊    | `PermissionsPage_Cap_Tts_Label`                        |
| Speech-to-text   | Microphone+lines  | Dictate      | F12E  | `Speech`                  | 🎤    | `PermissionsPage_Cap_Stt_Label`                        |

> Both the tray and app surfaces render the **Segoe Fluent** glyph
> (16 px in tray rows, 20 px in app `SettingsCard.HeaderIcon`). The
> emoji column is for documentation only.

## Sections / categories

| Concept     | Metaphor     | Segoe Fluent | Glyph | `FluentIconCatalog` const |
|-------------|--------------|--------------|-------|---------------------------|
| Permissions | Shield       | Shield       | EA18  | `Permissions`             |
| Sessions    | Speech bubble| Message      | E8BD  | `Sessions`                |
| Approvals   | Warning      | Warning      | E7BA  | `Approvals`               |
| Devices     | Two devices  | Devices      | E772  | `Devices`                 |
| System (this PC) | PC      | PC1          | E839  | `System`                  |

## Actions (tray flyout primary actions)

| Concept              | Metaphor        | Segoe Fluent | Glyph | `FluentIconCatalog` const |
|----------------------|-----------------|--------------|-------|---------------------------|
| Dashboard            | Globe           | Globe        | E774  | `Dashboard`               |
| Chat                 | Speech bubble   | Message      | E8BD  | `Chat`                    |
| Canvas (action)      | Artist palette  | Color        | E790  | `CanvasAct`               |
| Voice (action)       | Microphone      | Microphone   | E720  | `VoiceAct`                |
| Quick Send…          | Send arrow      | Send         | E724  | `QuickSend`               |
| Reconfigure… / Setup | Bank / building | Bank         | E825  | `Setup`                   |
| Companion Settings…  | Gear            | Settings     | E713  | `Settings`                |
| About                | Info circle     | Info         | E946  | `About`                   |
| Close                | Cancel ✕        | Cancel       | E711  | `Exit`                    |

## Affordances / status

| Concept       | Metaphor      | Segoe Fluent | Glyph | `FluentIconCatalog` const |
|---------------|---------------|--------------|-------|---------------------------|
| Sub-menu hint | Chevron right | ChevronRight | E76C  | `ChevronR`                |
| Check mark    | ✓             | CheckMark    | E73E  | `Check` / `StatusOk`      |
| Warning       | ⚠             | Warning      | E7BA  | `StatusWarn`              |
| Error         | ⊘             | ErrorBadge   | EA39  | `StatusErr`               |
| Hostname / system info | Device | Devices      | E977  | (literal in XAML)         |

## Rendering rules

- All Segoe Fluent glyphs are rendered through
  `FluentIconCatalog.Build(glyph, size)` so the icon honors the
  system-resolved `SymbolThemeFontFamily` (Segoe Fluent Icons on
  Win11, Segoe MDL2 Assets fallback on Win10).
- **Sizes:**
  - Tray menu row: 16 px
  - `SettingsCard.HeaderIcon` on a settings page: 20 px (toolkit default)
  - Page header decorations (hostname, info badge in cards): 16 px
- **Text scale:** every icon `TextBlock` / `FontIcon` must set
  `IsTextScaleFactorEnabled="False"` so user text scaling doesn't
  blow glyphs out of layout (winuxe Accessibility rule).
  `FluentIconCatalog.Build` handles this — if you author a
  `FontIcon` inline, set it yourself.
- **`SettingsCard.HeaderIcon` / `SettingsExpander.HeaderIcon` accept
  `IconElement` only** (`FontIcon`, `BitmapIcon`, `PathIcon`, …).
  A `Shape` like `Ellipse` or a `Rectangle` will fail XAML compile
  silently — put colored dots inside the `Header` content instead.
- **Color:** Default `Foreground` — do not hardcode a color. Status
  glyphs use the matching `SystemFillColorSuccessBrush /
  CautionBrush / CriticalBrush` (see `tokens.md`).
- **Reuse:** Two distinct concepts may share a glyph if and only if
  the metaphor itself is shared (Sessions / Chat both = Message;
  Browser / Dashboard both = Globe; Canvas-capability /
  Canvas-action both = Color). Add a code comment when reusing.

## Brand

The lobster (🦞) is a brand placeholder retained in
`FluentIconCatalog.Brand`. Do not use it for product concepts.
