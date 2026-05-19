# Tokens — Color, Typography, Spacing, Radius

Phase 1: tokens are **referenced by name**, not redefined. The names
are WinUI 3 ThemeResource keys (Fluent 2 system tokens) — the tray
and the app both render WinUI, so Fluent 2 is the only token system
in play.

Never hardcode colors, sizes, or radii in XAML. If a value is missing
from the table below, ask before introducing it.

## Color (theme-aware brushes)

### Surface / background

| Role                             | Token                                                  | Used for                                       |
|----------------------------------|--------------------------------------------------------|------------------------------------------------|
| Page background (acrylic/mica)   | (window backdrop; no brush)                            | Tray flyout window, Companion Settings shell.  |
| Card background                  | `CardBackgroundFillColorDefaultBrush`                  | Node status card, capability rows.             |
| Subtle / secondary card          | `SubtleFillColorSecondaryBrush`                        | STT engine details sub-card.                   |
| Card stroke                      | `CardStrokeColorDefaultBrush`                          | All card borders, dividers inside cards.       |
| Window border                    | `SurfaceStrokeColorDefaultBrush`                       | Tray flyout outer 1 px border.                 |

### Text

| Role                  | Token                              |
|-----------------------|------------------------------------|
| Primary text          | `TextFillColorPrimaryBrush` (default `Foreground`) |
| Secondary / caption   | `TextFillColorSecondaryBrush`      |
| Disabled              | `TextFillColorDisabledBrush`       |

### Status (status dots, badges, icons)

| State           | Token                              |
|-----------------|------------------------------------|
| Success / OK    | `SystemFillColorSuccessBrush`      |
| Caution / warn  | `SystemFillColorCautionBrush`      |
| Critical / err  | `SystemFillColorCriticalBrush`     |
| Neutral / off   | `SystemFillColorNeutralBrush`      |

> The "Node active" / "Gateway connected" green dot must always be
> `SystemFillColorSuccessBrush`, never a literal `Green`. Same goes for
> warning/error dots.

## Typography (WinUI text styles)

| Role                                 | Style                          |
|--------------------------------------|--------------------------------|
| Page title (`Permissions`)           | `TitleTextBlockStyle`          |
| Section header (`Capabilities`)      | `BodyStrongTextBlockStyle`     |
| Card primary line (`Node mode`)      | `BodyStrongTextBlockStyle`     |
| Card secondary description           | `CaptionTextBlockStyle` + `TextFillColorSecondaryBrush` |
| Row label (`Canvas`)                 | `BodyTextBlockStyle`           |
| Row description                      | `CaptionTextBlockStyle` + `TextFillColorSecondaryBrush` |
| Tray status / metric line            | `CaptionTextBlockStyle` + `TextFillColorSecondaryBrush` |
| Keyboard accelerator (`Ctrl+Alt+;`)  | `CaptionTextBlockStyle` + `TextFillColorSecondaryBrush`, right-aligned |

## Spacing

All spacing is on the **4 px grid** (winuxe Critical Rule #5). The
labels below are **documentation-only mnemonics** — they are NOT
ThemeResource keys. Emit raw `px` values in XAML; never write
`{StaticResource md}`.

| Mnemonic    | px | Used for                                                |
|-------------|----|---------------------------------------------------------|
| `xs`        | 4  | Title → meta line gap; description-to-element micro-spacing. |
| `sm`        | 8  | Card-to-card vertical spacing on Permissions page.      |
| `md`        | 12 | Inner card vertical spacing (`Spacing="12"`).           |
| `lg`        | 16 | Card row column spacing (icon ↔ text ↔ toggle).         |
| `page`      | 24 | Page outer padding.                                     |
| `flyout-x`  | 8  | Tray flyout outer horizontal padding.                   |
| `flyout-y`  | 8  | Tray flyout outer top padding (4 bottom).               |

## Radius

| Token                  | px | Used for                              |
|------------------------|----|---------------------------------------|
| `ControlCornerRadius`  | 4  | Default control rounding.             |
| `OverlayCornerRadius`  | 8  | Cards, tray flyout window border.     |

## Sizing

| Surface element          | Size                                       |
|--------------------------|--------------------------------------------|
| Tray menu row            | `MinHeight=32`, 16 px icon, 12 px gap.     |
| Permissions row icon     | 20 px (`SettingsCard.HeaderIcon` default). |
| Permissions card padding | 16 px inside the card.                     |
| Status dot               | 12 px diameter on cards, 8 px on tray.     |
| Tray flyout window       | `Width=320`, content `MaxHeight≈400`.      |
| Permissions page         | `MaxWidth=900`, page padding 24 px.        |

Rules:

- Use `MinHeight`/`MinWidth` and `MaxHeight`/`MaxWidth` — never
  fixed `Height`/`Width` on a text container (clips at large text
  scales — winuxe Layout rule).
- Minimum on-screen font size is `CaptionTextBlockStyle` (12 px).
  Do not go smaller (CJK legibility — winuxe Accessibility rule).
