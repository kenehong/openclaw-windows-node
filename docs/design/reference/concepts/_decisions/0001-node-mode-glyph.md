# CDR-0001 — Node mode card glyph

- **Status**: Accepted
- **Date**: 2026-05-15
- **Author**: kehong
- **Affects**: `docs/design/reference/iconography.md`,
  `src/OpenClaw.Tray.WinUI/Helpers/FluentIconCatalog.cs`,
  `src/OpenClaw.Tray.WinUI/Pages/PermissionsPage.xaml`.

## Context

The Permissions page Node-mode card and the iconography catalog
disagreed:

- Code (`PermissionsPage.xaml` line 45): `Glyph="&#xE839;"` (`PC1`).
- Catalog (`FluentIconCatalog.System`): `\uE7F4` (`TVMonitor`).
- `iconography.md` mirrored the catalog (TVMonitor).

Pick one and align both.

## Decision

Use **`\uE839` (PC1)** as the canonical glyph for the
*Node mode* / *this PC as a node* concept.

Rationale:

- `PC1` reads specifically as "a desktop PC", which matches the
  semantic of *"this computer is participating as a node"* better
  than `TVMonitor`, which is generic display hardware.
- It is the glyph users currently see; changing the code would be a
  visible regression for no benefit.
- The tray flyout currently uses no glyph for the same concept (it
  shows the status dot only), so no other surface is affected.

## Consequences

- `FluentIconCatalog.System` changes value from `\uE7F4` to `\uE839`,
  and its inline comment updates to `PC1`.
- `iconography.md` row "System (this PC)" updates accordingly.
- `concepts/states/node-mode.md` updates its Icon section.
- `PermissionsPage.xaml` migrates from the literal `&#xE839;` glyph
  to `{x:Bind helpers:FluentIconCatalog.System}` (or equivalent), so
  the constant is the single source of truth.

## Alternatives considered

- **Keep `\uE7F4` TVMonitor, change the code.** Rejected — user
  visible regression, and PC1 is the more accurate metaphor.
- **Introduce a new constant `FluentIconCatalog.NodeModeCard`**
  separate from `System`. Rejected — `System` already meant "this
  PC"; adding a near-duplicate constant fragments the catalog.
