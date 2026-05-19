# Reference: WinUI 3 Gallery

## What it is

**WinUI 3 Gallery** (formerly "WinUI Gallery", before that "XAML
Controls Gallery") is Microsoft's official sample app. Every WinUI
3 control ships with a live demo page, a copyable XAML snippet, a
copyable C# snippet, and links to the corresponding Microsoft Learn
documentation. Microsoft maintains it as the runnable companion to
the WinUI control documentation.

## Why we care

When the agent is unsure how a Win 11 Settings-style control should
look or behave (a `ToggleSwitch` inside a `SettingsCard`, an
`InfoBar` for inline status, a `NumberBox` in a settings row, a
`ContentDialog` for confirmations, …), the answer is: open the
Gallery, find the control, copy the canonical pattern, then apply
the OpenClaw concept (label, icon, copy) on top.

This avoids two common failure modes:

1. Reading API docs without seeing the rendered result and shipping
   a control that looks technically correct but visually wrong.
2. Inventing a new layout when an established Win 11 Settings
   pattern already exists in the Gallery.

## Canonical links

- **Microsoft Store** —
  https://apps.microsoft.com/detail/9P3JFPWWDZRC
  (search "WinUI 3 Gallery"; free, installs in seconds).
- **GitHub source** —
  https://github.com/microsoft/WinUI-Gallery
  - Each control page's XAML is in
    `WinUIGallery/ControlPages/<ControlName>Page.xaml`. Use this
    when you need to read the pattern offline.
- **Featured samples index** (Microsoft Learn) —
  https://learn.microsoft.com/en-us/windows/apps/get-started/samples

## How to use it during design / review

1. Install the Gallery app (one-time).
2. Navigate to **Design guidance → Settings pages** for the
   `SettingsCard` / `SettingsExpander` patterns directly.
3. Or navigate to a specific control (Inputs → `ToggleSwitch`,
   etc.) for the canonical row content.
4. Toggle **Light / Dark / High contrast** in the Gallery's theme
   switcher to verify token usage before shipping.
5. Copy the XAML snippet, then **strip Gallery-specific noise**
   (sample bindings, `<local:ControlExample>` wrappers) before
   pasting into OpenClaw code.

## Local mapping

| Gallery section                       | OpenClaw doc                              |
|---------------------------------------|-------------------------------------------|
| Design guidance → Settings pages      | `docs/design/reference/surfaces/permissions-page.md`|
| Inputs → `ToggleSwitch`, `ComboBox`   | `docs/design/reference/surfaces/permissions-page.md` row anatomy |
| Status & info → `InfoBar`             | (pattern for inline error / waiting state in `concepts/states/node-mode.md`) |
| Icons → `Segoe Fluent Icons`          | `docs/design/reference/iconography.md`              |
