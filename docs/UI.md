# UI Design Pointers

## Design Language

Follow **Windows 11 Fluent Design** — rounded corners, subtle shadows, Segoe UI Variable typography, restrained color. The app is a utility; it should feel lightweight and stay out of the way.

- Rounded corners: `CornerRadius="8"` on panels, `CornerRadius="4"` on buttons and inputs
- Acrylic/Mica backdrop on the overlay for the frosted-glass feel native to Win11
- No custom chrome — borderless windows only
- Icons: [Segoe Fluent Icons](https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font) (built into Windows 11)

---

## Color Palette

| Role | Light mode | Dark mode |
|---|---|---|
| Background (overlay) | `#F3F3F3` (acrylic tint) | `#202020` (acrylic tint) |
| Surface (panels) | `#FFFFFF` | `#2C2C2C` |
| Border | `#E0E0E0` | `#3D3D3D` |
| Primary text | `#1A1A1A` | `#F0F0F0` |
| Secondary / hint text | `#717171` | `#9D9D9D` |
| Accent (focus, buttons) | Windows `SystemAccentColor` | same |
| Warning background | `#FFF4CE` | `#3D3000` |
| Warning text | `#7A5800` | `#FFD966` |
| Error background | `#FDE7E9` | `#3D0009` |
| Error text | `#C42B1C` | `#FF99A4` |

QR codes are always black (`#000000`) on white (`#FFFFFF`) — not user-configurable.

---

## Typography

All text uses **Segoe UI Variable** (built into Windows 11).

| Role | Size | Weight | Usage |
|---|---|---|---|
| Body | 14 px | Regular | TextBox content, labels |
| Caption | 12 px | Regular | Warnings, byte count, hints |
| Button | 14 px | SemiBold | Button labels |
| Title | 20 px | SemiBold | Settings window header |
| Section header | 16 px | SemiBold | Settings sections |

---

## Overlay Window

The overlay opens on every capture. It must appear fast and require minimal interaction.

### Layout

```
┌──────────────────────────────────────────────┐
│  [⬡ OCR Region] (hidden by default)  [✕] Esc│  ← 32 px header
├─────────────────────┬────────────────────────┤
│                     │                        │
│  [Editable TextBox  │   QR code image        │
│   captured / OCR'd  │   (square, fills       │
│   text goes here]   │   column minus 16 px   │
│                     │   padding each side)   │
│  142 / 1 663 bytes  │                        │
├─────────────────────┴────────────────────────┤
│ ⚠ Warning / ✗ Error message (if any)        │  ← status bar, colored bg
└──────────────────────────────────────────────┘
```

### Sizing

- Default: **560 × 300 px**
- TextBox column: 50%, QR column: 50%
- Minimum: **400 × 240 px**
- Status bar: 32 px tall, always reserved (opacity toggled, no layout shift)

### Header Bar

- **OCR Region button** (left): hidden by default (`Visibility.Collapsed`); shown when `Overlay.ShowOcrButton = true` in settings. Triggers `RegionSelectorWindow`; icon is a crosshair glyph from Segoe Fluent Icons.
- **Close button** (right): `✕`, same as `Esc`; 32×32 px touch target

### TextBox

- Multi-line, `SpellCheck.IsEnabled="False"`
- Font: `Cascadia Mono` or `Consolas`, 13 px — monospace makes invisible characters visible
- Vertical scrollbar on overflow only
- Placeholder: `"Selected text will appear here…"`
- Byte count caption below-right: `"142 / 1 663 bytes"` (updates as user types)

### QR Image

- `Stretch="Uniform"`, `RenderOptions.BitmapScalingMode="NearestNeighbor"` — modules must be sharp pixels
- 1 px `#E0E0E0` border around the image area
- 4-module quiet zone included by QRCoder by default

### Status Bar

- Hidden (opacity 0) when nothing to report
- Warning (80–100% capacity): amber background — `⚠ Approaching QR capacity — consider reducing text or switching to ECC L`
- Error (>100% capacity): red background — `✗ Too much data — edit the text to reduce it`
- Fade in: 150 ms opacity transition

### Positioning

- Opens 16 px to the right of the mouse cursor, vertically aligned to cursor
- Clamped to current monitor work area
- Flips to the left of the cursor if insufficient space to the right

---

## Region Selector Window

Shown when the user clicks **OCR Region** in the overlay. Covers the full virtual screen (all monitors). The overlay hides itself before this window appears and re-shows after selection completes or is cancelled.

### Layout

```
┌──────────────────────────────────────────────────────────────┐
│                                                              │
│   (40% opacity dark overlay across entire screen)           │
│                                                              │
│        ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐                            │
│          [selection rectangle]                               │
│        │   white border, 2 px  │                            │
│         ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─                             │
│                                                              │
│   "Draw a region to read text from.  Esc to cancel."        │
│   (instruction text, centered, caption size, white)          │
└──────────────────────────────────────────────────────────────┘
```

### Behaviour

- Cursor: `Cursors.Cross`
- Mouse down: begin selection rectangle
- Mouse move: update rectangle in real time
- Mouse up: if region is > 4×4 px, capture and OCR it; otherwise discard
- `Esc`: cancel — overlay re-shows with its previous text unchanged
- No keyboard input other than `Esc`

### Visual Style

- Background: `#00000066` (40% black) over full screen
- Selection rectangle: 2 px solid white border; interior fill `#FFFFFF22` (very faint)
- Instruction text: white, 13 px, centered horizontally at 80% vertical position
- No window chrome, no title bar

---

## Settings Window

A standard modal dialog opened from the tray right-click menu. Not a floating overlay.

### Layout

```
┌──────────────────────────────────────────────────┐
│  Settings                                    [✕] │  ← 48 px title bar
├──────────────────────────────────────────────────┤
│                                                  │
│  Hotkey                                          │
│  ┌──────────────────────────────────────────┐   │
│  │  Ctrl + Shift + Q          [Click to set] │   │
│  └──────────────────────────────────────────┘   │
│                                                  │
│  QR Code                                         │
│  Size      [━━━━●━━━━━━━] 300 px  [QR preview]  │
│  ECC Level [Q ▾]  ⓘ                             │
│                                                  │
│  Overlay                                         │
│  Auto-dismiss  [✓]  after  [5]  seconds          │
│  [○] Show OCR Region button in overlay           │  ← toggle switch (default off)
│                                                  │
│  Startup                                         │
│  [✓]  Launch QrApp when Windows starts           │
│                                                  │
│  Symbol Filter                                   │
│  ┌──────────────────────────────────────────┐   │
│  │ Match       Replace   Regex    [Delete]   │   │
│  │ ﻿      (empty)   □        [🗑]       │   │
│  │ \r\n        \n        □        [🗑]       │   │
│  │ \s+$        (empty)   ✓        [🗑]       │   │
│  └──────────────────────────────────────────┘   │
│  [+ Add rule]                                    │
│                                                  │
├──────────────────────────────────────────────────┤
│                          [Cancel]  [Apply]        │  ← 48 px footer
└──────────────────────────────────────────────────┘
```

### Sizing

- Fixed width: **480 px**
- Height: auto, min **500 px**, max **80% of screen height** with scroll

### Hotkey Field

- Shows current hotkey as text: `Ctrl + Shift + Q`
- Click → recording mode: accent tint background, text `"Press a key combination…"`
- `KeyDown`: capture modifiers + key, display immediately, exit recording mode
- Conflict: inline error `"This hotkey is in use by another application"`
- `Esc` during recording: cancel without changing the current hotkey

### Size Slider

- Range 200–600, step 50; tick marks at each step
- Current value label to the right: `300 px`
- Small live QR preview thumbnail to the right of the label, updates as slider moves

### ECC ComboBox

- Options: `L — 7% recovery`, `M — 15% recovery`, `Q — 25% recovery (default)`, `H — 30% recovery`
- ⓘ tooltip: `"Higher recovery = smaller data capacity. Q is a good default."`

### Toggle Switches (Overlay and Capture sections)

Toggle switches use a custom `CheckBox` template styled as a Windows 11-style pill toggle (40×20 px track, `#CCCCCC` off / `#0078D4` on, 16 px white thumb). The template is defined as `Style x:Key="ToggleSwitch"` in `SettingsWindow.xaml` resources.

| Toggle | Default | Behaviour |
|---|---|---|
| Show OCR Region button | Off | Controls `Visibility.Collapsed/Visible` of the OCR button in `OverlayWindow` |

### Startup Checkbox

- Label: `"Launch QrApp when Windows starts"`
- Checked by default
- On Apply: writes/removes registry key `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run\QrApp`

### Symbol Filter List

- Each row: `TextBox` (match), `TextBox` (replacement), `CheckBox` (regex), `Button` (delete)
- Non-printable characters displayed as Unicode escapes (`﻿`, `​`, etc.) — never raw invisible characters
- `[+ Add rule]` appends a blank row and focuses the Match field

### Footer

- **Cancel**: discards working copy, closes window
- **Apply**: validates, saves `settings.json`, applies all settings live (hotkey re-registers, autostart registry updated), closes window
- Apply is `IsDefault="True"` (`Enter` triggers it); `Esc` triggers Cancel

---

## System Tray Icon

- 16×16 px + 32×32 px for high-DPI
- QR code glyph in system foreground color (adapts to light/dark taskbar)
- Tooltip: `"QrApp — Ctrl+Shift+F2 to capture"`
- Right-click only:
  ```
  Settings
  ─────────
  Quit
  ```

---

## Interaction States

| Control | Default | Hover | Focused | Disabled |
|---|---|---|---|---|
| Button | Outlined | Light fill | Accent border | 40% opacity |
| TextBox | Subtle border | Border darkens | Accent border, 2 px | 40% opacity |
| Slider thumb | Accent fill | Scale 110% | Accent + ring | 40% opacity |
| CheckBox | System default | — | System default | 40% opacity |

---

## Accessibility

- All interactive controls have `AutomationProperties.Name` set
- Overlay `Image` has `AutomationProperties.Name` = the encoded text (so screen readers can announce it)
- Tab order follows visual reading order (top-to-bottom, left-to-right)
- Minimum click/touch target: 32×32 px
- State is never communicated by color alone — always paired with an icon or text label
