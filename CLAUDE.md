# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

QrApp is a .NET 8 WPF tray utility for Windows 11 that generates QR codes from selected screen text. The app has no main window — it lives in the system tray and shows a borderless floating overlay on hotkey press.

## Key Commands

```bash
# Build (debug)
dotnet build src/QrApp

# Run during development
dotnet run --project src/QrApp

# Publish — self-contained single-file EXE (the ONLY supported distribution format)
dotnet publish src/QrApp -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -o publish/
```

No test suite — verification is manual (see `tasks/todo.md` Phase 5 smoke tests).

## Architecture

**TFM:** `net8.0-windows10.0.22000.0` — Windows 11 only. The Windows SDK TFM gives direct access to `Windows.Media.Ocr` WinRT APIs without extra NuGet packages.

**No DI container.** All services are composed manually in `App.xaml.cs`.

### Capture pipeline (triggered by global hotkey)

```
HotkeyService → SelectionService (SendInput Ctrl+C → clipboard poll)
                    ↓ empty?
              OcrService.RecognizeCursorRegionAsync()   ← auto OCR fallback
                    ↓
              TextSanitizerService.Sanitize()           ← strip BOM, CRLF, zero-width chars etc.
                    ↓
              QrCodeService.Generate()                  ← QRCoder, black on white, TargetSizePx→PixelsPerModule
                    ↓
              OverlayWindow (borderless WPF, near cursor)
```

**Manual OCR path** (user clicks OCR button in overlay):
`OverlayWindow` hides → `RegionSelectorWindow` (fullscreen snip-style) → `OcrService.RecognizeRegionAsync(rect)` → same sanitize/generate pipeline → overlay re-shows.

### Key design decisions

- **`SelectionService`** synthesises `Ctrl+C` via `SendInput`, polls clipboard for 300 ms, then restores original clipboard contents. Must run on UI thread (STA).
- **`QrCodeService`** derives `PixelsPerModule` from `data.ModuleMatrix.Count` after QR generation so the output matches `TargetSizePx` regardless of QR version. Output is always black on white — colors are not configurable.
- **`SettingsService`** resets `settings.json` to defaults silently on any load exception (missing, locked, malformed JSON). Autostart is applied via `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`.
- **`HotkeyService`** uses a hidden `HwndSource` (message-only window) for `RegisterHotKey`. If the hotkey is pressed while an overlay is already open, the existing overlay closes and a fresh capture runs.
- **`OverlayViewModel`** debounces `SourceText` changes by 150 ms before regenerating the QR.
- **Status bar thresholds:** warning at 80–100% of QR v40 capacity; error above 100%.

### Settings schema (`%APPDATA%\QrApp\settings.json`)

```json
{
  "hotkey": { "modifiers": "Ctrl+Shift", "key": "Q" },
  "qr": { "targetSizePx": 300, "eccLevel": "Q" },
  "overlay": { "autoDismissSeconds": 0 },
  "autostart": true,
  "sanitizer": { "rules": [...] }
}
```

## Task Tracking

Active development tasks are in `tasks/todo.md`. Lessons from mistakes are in `tasks/lessons.md` — read them at session start. After any user correction, add a new entry to `tasks/lessons.md`.

---

# Workflow Orchestration
## 1. Plan Mode Default
- Enter plan mode for ANY non-trivial task (3+ steps or architectural decisions)
- If something goes sideways, STOP and re-plan immediately
- Use plan mode for verification steps, not just building
- Write detailed specs upfront to reduce ambiguity

## 2. Subagent Strategy
- Use subagents liberally to keep main context window clean
- Offload research, exploration, and parallel analysis to subagents
- For complex problems, throw more compute at it via subagents
- One task per subagent for focused execution

## 3. Self-Improvement Loop
- After ANY correction from the user: update tasks/lessons.md with the pattern
- Write rules for yourself that prevent the same mistake
- Ruthlessly iterate on these lessons until mistake rate drops
- Review lessons at session start for relevant project

## 4. Verification Before Done
- Never mark a task complete without proving it works
- Diff behavior between main and your changes when relevant
- Ask yourself: "Would a staff engineer approve this?"
- Run tests, check logs, demonstrate correctness

## 5. Demand Elegance (Balanced)
- For non-trivial changes: pause and ask "is there a more elegant way?"
- If a fix feels hacky: "Knowing everything I know now, implement the elegant solution"
- Skip this for simple, obvious fixes -- don't over-engineer
- Challenge your own work before presenting it

## 6. Autonomous Bug Fixing
- When given a bug report: just fix it. Don't ask for hand-holding
- Point at logs, errors, failing tests -- then resolve them
- Zero context switching required from the user
- Go fix failing CI tests without being told how

## Task Management
1. Plan First: Write plan to tasks/todo.md with checkable items
2. Verify Plan: Check in before starting implementation
3. Track Progress: Mark items complete as you go
4. Explain Changes: High-level summary at each step
5. Document Results: Add review section to tasks/todo.md
6. Capture Lessons: Update tasks/lessons.md after corrections

## Core Principles
1. Simplicity First: Make every change as simple as possible. Impact minimal code.
2. No Laziness: Find root causes. No temporary fixes. Senior developer standards.
3. Minimal Impact: Changes should only touch what's necessary. Avoid introducing bugs.
