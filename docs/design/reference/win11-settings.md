# Reference: Win 11 Settings app + Fluent 2

## What it is

The Windows 11 Settings app is Microsoft's flagship example of the
Fluent 2 design system applied to a WinUI 3 / WinAppSDK desktop
surface. Fluent 2 is the Microsoft cross-product design system that
defines color, type, spacing, motion, and component patterns for
modern Windows / Microsoft 365 surfaces.

## Why we care

OpenClaw Windows node app pages (Companion Settings window:
Permissions, Connection, Chat page, …) are modeled directly on the
Win 11 Settings visual language:

- Same control set (`SettingsCard`, `SettingsExpander`, `ToggleSwitch`,
  `ComboBox` inside cards, etc.).
- Same Fluent 2 ThemeResource tokens (`CardBackgroundFillColor*Brush`,
  `TextFillColor*Brush`, `SystemFillColor*Brush`).
- Same typography (`BodyStrongTextBlockStyle`, `BodyTextBlockStyle`,
  `CaptionTextBlockStyle`).
- Same rounded corners, spacing, padding.

By aligning to Win 11 Settings we get free dark / light /
high-contrast theming, system accent color, accessibility contrast
ratios, and a visual idiom users already understand.

## Canonical links

- **Fluent 2 design system** — https://fluent2.microsoft.design/
  - Component library, design tokens, motion, accessibility.
- **Design and code Windows apps (Microsoft Learn)** —
  https://learn.microsoft.com/en-us/windows/apps/design/
  - Top-level entry for Windows desktop design guidance.
- **Windows 11 design principles** —
  https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/design-principles
- **Layout and spacing for Windows apps** —
  https://learn.microsoft.com/en-us/windows/apps/design/layout/
- **Theme resources (color / brushes)** —
  https://learn.microsoft.com/en-us/windows/apps/design/style/xaml-theme-resources
- **Typography ramp (Segoe UI Variable)** —
  https://learn.microsoft.com/en-us/windows/apps/design/style/typography
- **Segoe Fluent Icons catalog** —
  https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font

## Local mapping

| Upstream concept           | OpenClaw doc                                                    |
|----------------------------|-----------------------------------------------------------------|
| Fluent 2 tokens            | `docs/design/reference/tokens.md`                                         |
| Segoe Fluent Icons catalog | `docs/design/reference/iconography.md`                                    |
| Win 11 Settings layout     | `docs/design/reference/surfaces/permissions-page.md`                      |
| Why we adopted this idiom  | [`cdr-001-adopt-win11-settings.md`](./cdr-001-adopt-win11-settings.md) |

## How we deviate

- We don't have a left nav rail — the app is a single-window
  surface launched from the tray.
- We render two surface families (tray flyout + app page); the tray
  flyout is denser than anything in Win 11 Settings and uses
  smaller (16 px) icons.
- We don't use Win 11 Settings' "category card" pattern (large
  illustrated entry points) — pages are flat lists of cards.
