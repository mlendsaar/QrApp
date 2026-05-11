# UI Design Pointers

## Design Language

Follow the **Windows 11 Fluent Design** conventions — rounded corners, subtle shadows, Segoe UI Variable typography, and restrained use of color. The app is a utility; it should feel lightweight and stay out of the way.

- Rounded corners: `CornerRadius="8"` on panels, `CornerRadius="4"` on buttons and inputs
- Acrylic/Mica backdrop on the overlay window for the frosted-glass feel native to Win11
- No custom chrome — borderless windows only; mimic system style through spacing and shadow
- Icons: [Segoe Fluent Icons](https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-fluent-icons-font) (built into Windows 11); fall back to Segoe MDL2 Assets on Windows 10

---

## Color Palette

| Role | Light mode | Dark mode |
|---|---|---|
| Background (overlay) | `#F3F3F3` (acrylic tint) | `#202020` (acrylic tint) |
| Surface (cards/panels) | `#FFFFFF` | `#2C2C2C` |
| Border | `#E0E0E0` | `#3D3D3D` |
| Primary text | `#1A1A1A` | `#F0F0F0` |
| Secondary text / hint | `#717171` | `#9D9D9D` |
| Accent (buttons, focus) | Windows system accent color (`SystemAccentColor`) | same |
| Warning background | `#FFF4CE` | `#3D3000` |
| Warning text | `#7A5800` | `#FFD966` |
| Error background | `#FDE7E9` | `#3D0009` |
| Error text | `#C42B1C` | `#FF99A4` |

Use the system accent color for interactive elements (focused `TextBox` border, primary button fill) so the app respects the user's Windows color preference.

---

## Typography

All text uses **Segoe UI Variable** (built into Windows 11).

| Role | Size | Weight | Usage |
|---|---|---|---|
| Body | 14 px | Regular | Source text `TextBox`, labels |
| Caption | 12 px | Regular | Warnings, character count, hints |
| Button | 14 px | SemiBold | Button labels |
| Title (Settings) | 20 px | SemiBold | Settings window header |
| Section header | 16 px | SemiBold | Settings section labels |

---

## Overlay Window

The overlay is the primary surface the user sees on every capture. It must open fast, be immediately readable, and require minimal interaction.

### Layout

```
┌─────────────────────────────────────────┐
│                                 [✕] Esc │  ← 32 px header bar, close button right
├───────────────────┬─────────────────────┤
│                   │                     │
│  [Editable text   │    QR code image    │
│   box — captured  │    (square, fills   │
│   or OCR'd text]  │    available space) │
│                   │                     │
│                   │                     │
├───────────────────┴─────────────────────┤
│ ⚠ Warning / ✗ Error message (if any)   │  ← single line, colored background
└─────────────────────────────────────────┘
```

### Sizing

- Default window size: **560 × 300 px**
- TextBox column: **50% width**, QR column: **50% width**
- QR image: square, fills its column minus 16 px padding on each side
- Minimum window size: **400 × 240 px**

### TextBox

- Multi-line, no spell-check (`SpellCheck.IsEnabled="False"`)
- Monospace font (`Cascadia Mono` or `Consolas`, 13 px) so non-printing characters and symbols are visible
- Vertical scrollbar visible only when content overflows
- Placeholder text: `"Selected text will appear here…"`
- Character count shown bottom-right of the TextBox in caption style: `"142 / 1 663 bytes"`

### QR Image

- `Stretch="Uniform"`, `RenderOptions.BitmapScalingMode="NearestNeighbor"` — QR modules must be sharp pixels, never blurry
- Thin 1 px `#E0E0E0` border around the image area
- White padding of at least 4 modules (quiet zone) around the QR — QRCoder includes this by default

### Status Bar (warning/error)

- Hidden when there is nothing to report
- Single line, 32 px tall, full width
- Warning (80–100% capacity): amber background, `⚠ Approaching QR capacity — consider reducing text or switching to ECC L`
- Error (> 100% capacity): red background, `✗ Too much data — edit the text to reduce it`
- Transition: fade in (150 ms opacity), no layout shift (reserve the height always, just toggle opacity)

### Positioning

- Opens 16 px to the right of the mouse cursor horizontally, aligned to cursor vertically
- Clamped to the current monitor's work area (never off-screen)
- If insufficient space to the right, flip to the left of the cursor

---

## Settings Window

A conventional modal dialog, not a floating overlay. Opens centered on the primary monitor (or centered on the parent if the tray window has a handle).

### Layout

```
┌──────────────────────────────────────────────────┐
│  Settings                                    [✕] │  ← 48 px title bar
├──────────────────────────────────────────────────┤
│                                                  │
│  Hotkey                                          │  ← section header
│  ┌──────────────────────────────────────────┐   │
│  │  Ctrl + Shift + Q          [Click to set] │   │
│  └──────────────────────────────────────────┘   │
│                                                  │
│  QR Code                                         │
│  Size          [━━━━●━━━━━━━━━] 300 px          │
│  ECC Level     [Q ▾]  ⓘ                         │
│  Foreground    [■] #000000                       │
│  Background    [□] #FFFFFF                       │
│                                                  │
│  Overlay                                         │
│  Auto-dismiss  [✓]  after  [5]  seconds          │
│                                                  │
│  Symbol Filter                                   │
│  ┌──────────────────────────────────────────┐   │
│  │ Match         Replace     Regex  [Delete] │   │
│  │ ﻿             (empty)     □      [🗑]     │   │
│  │ \r\n          \n          □      [🗑]     │   │
│  │ \s+$          (empty)     ✓      [🗑]     │   │
│  └──────────────────────────────────────────┘   │
│  [+ Add rule]                                    │
│                                                  │
├──────────────────────────────────────────────────┤
│                          [Cancel]  [Apply]        │  ← 48 px footer
└──────────────────────────────────────────────────┘
```

### Sizing

- Fixed width: **480 px**
- Height: auto (content-driven), min **480 px**, max **80% of screen height** with scroll

### Hotkey Field

- Displays current hotkey as text (`Ctrl + Shift + Q`)
- On click: enters recording mode — background shifts to accent color tint, text changes to `"Press a key combination…"`
- On `KeyDown`: capture modifiers + key, display immediately, exit recording mode
- If the key combo is already taken: show inline error `"This hotkey is in use by another application"`
- `Esc` cancels recording without changing the value

### Size Slider

- Range 200–600, step 50
- Tick marks at 200, 300, 400, 500, 600
- Current value shown as a label to the right: `300 px`
- Below the slider, a small live preview thumbnail of a sample QR updates as the slider moves

### ECC ComboBox

- Options: `L — 7% recovery`, `M — 15% recovery`, `Q — 25% recovery (default)`, `H — 30% recovery`
- Tooltip on the ⓘ icon: `"Higher recovery = smaller data capacity. Q is a good default."`

### Color Pickers

- Clicking the color swatch opens the system `ColorDialog` (WPF: use `System.Windows.Forms.ColorDialog` via interop, or a lightweight inline color picker)
- Swatch is a 24×24 px square with a 1 px border
- Hex value shown as editable text next to the swatch

### Symbol Filter List

- Each row: `TextBox` (match), `TextBox` (replacement), `CheckBox` (regex), `Button` (delete)
- Non-printable characters shown as their escape sequences (`\r\n`, `​`, etc.) — do not display raw invisible characters in the UI
- Rows are reorderable via drag handle (optional for v1; document as v2)
- `[+ Add rule]` appends a blank row and focuses the Match field

### Footer Buttons

- **Cancel**: closes window, discards all unsaved changes
- **Apply**: validates inputs, saves `settings.json`, applies changes live (re-registers hotkey, updates QR settings), closes window
- Apply is the default button (`IsDefault="True"`); `Enter` triggers it
- `Esc` triggers Cancel

---

## System Tray Icon

- 16×16 px icon, with a 32×32 px version for high-DPI
- Simple QR code glyph in the system foreground color (adapts to light/dark taskbar)
- Right-click context menu:
  ```
  Settings
  ─────────
  Quit
  ```
- No left-click action (avoids conflict with hotkey); tooltip shows `"QrApp — Ctrl+Shift+Q to capture"`

---

## Interaction States

| Control | Default | Hover | Focused | Disabled |
|---|---|---|---|---|
| Button (secondary) | Outlined | Light fill | Accent border | 40% opacity |
| TextBox | Subtle border | Border darkens | Accent border, 2 px | 40% opacity |
| Slider thumb | Accent fill | Scale 110% | Accent + ring | 40% opacity |
| CheckBox | System default | — | System default | 40% opacity |

---

## Accessibility

- All interactive controls have `AutomationProperties.Name` set
- Overlay `Image` has `AutomationProperties.Name` = the encoded text content
- Tab order follows visual reading order (left-to-right, top-to-bottom)
- Minimum touch/click target: 32×32 px
- Color is never the sole indicator of state — always pair with an icon or text
