# CDR-001 — Adopt the Win 11 Settings idiom for OpenClaw app pages

> **Status:** Accepted
> **Date:** 2026-05-15
> **Decider:** kehong + Copilot CLI (design refactor of `PermissionsPage`)
> **Scope:** App-page surfaces in `OpenClaw.Tray.WinUI` (Permissions,
> Connection, Chat page, future settings pages). Tray flyouts are
> out of scope — they retain their own dense popup idiom.

## Context

Before this decision, `PermissionsPage.xaml` rendered every row as
a hand-rolled `Border + Grid` with literal colors
(`Microsoft.UI.Colors.Gray / LimeGreen / Orange`) on status dots
and emoji glyphs (📷, 🖼️, 🖥️) as row icons. Visually the page
diverged from Win 11 Settings, and from itself: each row's
`Padding`, `CornerRadius`, divider, and toggle alignment had to be
re-specified by hand.

Other app pages (Connection, Cron, Hub) showed the same drift —
each was a snowflake.

## Decision

App-page surfaces in `OpenClaw.Tray.WinUI` follow the **Win 11
Settings** visual idiom:

1. Rows are `CommunityToolkit.WinUI.Controls.SettingsCard`.
2. Master-toggle rows with revealable sub-content are
   `CommunityToolkit.WinUI.Controls.SettingsExpander`.
3. Icons are Segoe Fluent glyphs from `FluentIconCatalog`,
   rendered through `FontIcon` in the `HeaderIcon` slot — never
   emoji, never hand-placed.
4. Colors come from Fluent 2 ThemeResource brushes
   (`SystemFillColor{Neutral,Success,Caution,Critical}Brush`,
   `CardBackgroundFillColorDefaultBrush`,
   `TextFillColor{Primary,Secondary}Brush`, …) — never
   `Microsoft.UI.Colors.*` literals.
5. Typography comes from the Fluent 2 type ramp
   (`BodyTextBlockStyle`, `BodyStrongTextBlockStyle`,
   `CaptionTextBlockStyle`).

This is recorded as a **system-wide** decision, not a per-concept
one, because it constrains every future app page.

## Alternatives considered

| Option                                            | Why rejected |
|---------------------------------------------------|--------------|
| Keep hand-rolled `Border + Grid` rows             | Each page becomes a snowflake; theming, accessibility, HC support all DIY. |
| Build our own settings-card control internally    | Reinvents `SettingsCard`. Maintenance cost without value. |
| Adopt Material 3 / Fluent 1 / a bespoke palette   | Mismatches the host OS; users would feel two design systems in one app. |
| Use only stock WinUI `Expander` / `Border`        | Doesn't render the Win 11 Settings row chrome (rounded corners, icon column, description column, content slot, hover/pressed). |

## Consequences

### Positive

- Free dark / light / high-contrast theming.
- Free system-accent color reflection.
- Visual consistency between OpenClaw app pages and the host OS
  Settings app — users already understand the layout.
- Lower per-page authoring cost; one `SettingsCard` per row.
- A clear bar for code review: "did you use `SettingsCard`?"

### Negative

- Adds a NuGet dependency
  (`CommunityToolkit.WinUI.Controls.SettingsControls`, currently
  pinned to 8.2.251219). Upstream package upgrades need to be
  vetted before adoption.
- We cannot fully customize internal padding / divider thickness —
  we live with the toolkit's defaults.
- Some legacy custom rows (STT/TTS/MCP/ExecPolicy details in
  `PermissionsPage.xaml`) remain in their old form until they are
  migrated. Mixed-idiom pages are visually obvious during the
  transition.

## Validation

This CDR was validated by redesigning `PermissionsPage.xaml`:

- `NodeStatusCard` Border → `tk:SettingsExpander` with master
  toggle as `Content` and a status sub-row as a child
  `tk:SettingsCard`.
- All capability rows (Camera, Canvas, Browser, Screen, Location,
  Voice, Speech) → `tk:SettingsCard` built in code-behind
  (`BuildCapabilityRow`).
- Status dot fills migrated from literal colors to
  `SystemFillColor*Brush`.
- Build + 1548 Shared tests + 1189 Tray tests all green.

## References

- [`win11-settings.md`](./win11-settings.md) — what we adopted from.
- [`settings-controls.md`](./settings-controls.md) — the controls
  used to implement the idiom.
- [`winui-gallery.md`](./winui-gallery.md) — runnable examples to
  consult during implementation.
- `docs/design/surfaces/permissions-page.md` — first surface
  implementing this CDR.
- `docs/design/concepts/_decisions/0001-node-mode-glyph.md` —
  concept-level CDR (Node mode glyph) created in the same pass.
