# Concept: `camera`

> Lets agents request still images and short clips from this PC's
> camera through the gateway.

## Canonical name

`Camera`. Sentence-case. Not "Webcam", not "Video".

## Copy

- **Short label**: `Camera` / status form `camera`.
- **One-liner** (Permissions row description):
  > Lets agents capture still images and short clips from this PC's
  > camera.
- **Off state copy**: same one-liner.

## Icon

- Concept / metaphor: **Camera** (front-facing photo camera).
- Segoe Fluent: `Camera` (U+E722).
- `FluentIconCatalog` const: `Camera`.
- Emoji (docs reference only): 📷

## States

| State                | Visual                                  | Copy                                  |
|----------------------|-----------------------------------------|---------------------------------------|
| enabled              | toggle on, color camera icon            | one-liner                             |
| disabled             | toggle off                              | one-liner                             |
| Node mode off        | row dimmed to 40 % opacity              | one-liner                             |
| OS permission denied | (TBD) — toggle disabled, hint to Windows Settings → Privacy → Camera | "Allow camera access in Windows Settings" |

## Appears in

- `surfaces/permissions-page.md` — capability row, position 2 of 7.
- Tray Permissions flyout — mirror row.
- Onboarding wizard capability list.

## Code anchors

- Setting flag: `Settings.NodeCameraEnabled`.
- Resw keys (en-US):
  - `PermissionsPage_Cap_Camera_Label` → `Camera`
  - `PermissionsPage_Cap_Camera_Description` → see one-liner.
- XAML / code-behind: `BuildCapabilityToggles` in
  `Pages/PermissionsPage.xaml.cs` (capability tuple `(📷, …)`).
- Status line wire string: literal `"camera"` in
  `App.xaml.cs` capability-list builder.

## Do / Don't

- ✅ Pair with a clear OS-permission hint when the underlying Windows
  camera permission is denied. Don't silently fail.
- ❌ Don't ship a separate "Webcam" or "Video" capability — extend
  this one with sub-options if needed (and add a new concept file).
