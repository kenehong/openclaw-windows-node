# Concept: `<concept-id>`

> **One-line summary** of what the concept is, in plain language.

## Canonical name

`Exact Label` — the only form that may appear in UI. Synonyms that must
**not** be used: …

## Copy

- **Short label** (chip, status line, row): `…`
- **One-liner** (capability row description, tooltip): `…`
- **Long description** (settings page, onboarding card): `…`
- **Empty / off state**: `…`
- **Error / denied state**: `…`

## Icon

- Concept / metaphor: `…` (e.g. "Artist palette")
- Segoe Fluent: `Color` (U+E790) — see [iconography](../iconography.md)
- `FluentIconCatalog` const: `Canvas` / `CanvasAct`
- Emoji (docs reference only): 🎨

## States

| State    | Visual                            | Copy                       |
|----------|-----------------------------------|----------------------------|
| enabled  | toggle on, color icon             | one-liner                  |
| disabled | toggle off, icon at 60% opacity   | one-liner                  |
| denied   | toggle off + lock badge           | "Blocked by administrator" |
| missing  | row hidden                        | —                          |

## Appears in

- `surfaces/tray-flyout.md` — row `Canvas`, primary actions section
- `surfaces/permissions-page.md` — capability row, position 3
- (and so on — must agree with `surfaces-index.md`)

## Code anchors

- Setting flag(s): `Settings.NodeCanvasEnabled`
- Resw keys: `PermissionsPage_Cap_Canvas_Label`, `PermissionsPage_Cap_Canvas_Description`
- XAML: `Pages/PermissionsPage.xaml.cs` (`BuildCapabilityToggles`)
- Tray flyout: `App.xaml.cs` (search for `"Canvas"`)
- Status line wire string: `"canvas"` (used in `Providing N capabilities: …`)
- Telemetry: `capability.canvas.toggled` (TBD)

## Do / Don't

- ✅ Use the canonical label everywhere.
- ✅ Use the metaphor icon at every size.
- ❌ Don't translate the product term.
- ❌ Don't hide the row instead of showing the disabled state — unless
  the gateway has not advertised the surface.
