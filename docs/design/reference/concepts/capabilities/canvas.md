# Concept: `canvas`

> Shared visual surface hosted on this PC that agents can render to
> and interact with. Used by the A2UI canvas window and the tray's
> `Canvas` quick action.

## Canonical name

`Canvas` — single word, sentence-case in row labels, Title Case in
window titles. Do **not** call it Whiteboard, Drawing, or Shared View.
The product term is not translated; locale-specific resw values exist
but ship as the same string in English.

## Copy

- **Short label** (tray menu, status string): `Canvas` / `canvas` (lower
  in the `Providing N capabilities: …` list).
- **One-liner** (Permissions row description, capability toggle):
  > Lets agents render and interact with a shared visual canvas hosted
  > on this PC.
- **Window title** (`A2UICanvasWindow`): defaults to `Canvas`; agents
  may override via the `title` arg of a `canvas.open` request.
- **Off state copy** (same one-liner — we don't editorialize when off).
- **Denied / missing**: not yet defined; the row is hidden if Node mode
  is off (whole capability list dimmed at 40 % opacity).

## Icon

- Concept / metaphor: **Artist palette**.
- Segoe Fluent: `Color` (U+E790) — see [iconography](../../iconography.md).
- `FluentIconCatalog` consts: `Canvas` (capability) and `CanvasAct`
  (action) — both alias the same glyph by design.
- Emoji (docs reference only): 🎨

## States

| State                    | Visual                                     | Copy                                       |
|--------------------------|--------------------------------------------|--------------------------------------------|
| enabled (Node on, toggle on)  | toggle on, palette icon at full color      | one-liner                                  |
| disabled (toggle off)         | toggle off, row at default opacity         | one-liner                                  |
| Node mode off                 | row dimmed to 40 % opacity, toggle frozen  | one-liner (greyed)                         |
| denied                        | (TBD) lock badge, toggle disabled          | "Blocked by administrator" (TBD)           |

The `Node active` summary line lists `canvas` in
`Providing N capabilities: …` exactly when this toggle is on AND Node
mode is on. See `concepts/states/node-mode.md`.

## Appears in

- `surfaces/tray-flyout.md` — primary action row "Canvas".
- `surfaces/permissions-page.md` — capability row, position 3 of 7
  (`browser, camera, **canvas**, screen, location, tts, stt`).
- Tray Permissions flyout (mirror of the in-app page).
- Onboarding wizard capability list.
- (See `surfaces-index.md` for the parity matrix.)

## Code anchors

- Setting flag: `Settings.NodeCanvasEnabled` (bool).
- Resw keys (en-US):
  - `PermissionsPage_Cap_Canvas_Label` → `Canvas`
  - `PermissionsPage_Cap_Canvas_Description` → see one-liner above.
- XAML / code-behind: `src/OpenClaw.Tray.WinUI/Pages/PermissionsPage.xaml.cs`,
  in `BuildCapabilityToggles` (capability tuple `(🎨, …)`).
- Tray quick action: `App.xaml.cs`, `menu.AddMenuItem("Canvas",
  FluentIconCatalog.Build(FluentIconCatalog.CanvasAct), "canvas")`.
- Status line wire string: literal `"canvas"` in `App.xaml.cs`
  (`if (hub.Settings?.NodeCanvasEnabled == true) caps.Add("canvas");`).
- A2UI window: `src/OpenClaw.Tray.WinUI/Windows/A2UICanvasWindow.xaml`,
  hosted by `A2UI/Hosting/SurfaceHost.cs`.
- Capability handler: `src/OpenClaw.Shared/Capabilities/CanvasCapability.cs`
  (`canvas.open`, default title `"Canvas"`).

## Do / Don't

- ✅ Use 🎨 / `\uE790` everywhere — same metaphor across surface sizes.
- ✅ Hyperlink the action label to the same canvas window the
  capability serves; do not spawn a second view type.
- ❌ Don't rename the window title to "Drawing" or "Whiteboard" even
  if a designer's mock says so — open a Concept Decision Record first.
- ❌ Don't add a separate `Canvas` capability row for a different
  surface kind; if you need that, it is a new concept and gets its own
  file.
