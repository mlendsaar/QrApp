# Lessons — QrApp

Patterns and mistakes captured during development. Review at the start of each session.

---

## Documentation Phase Lessons

**Lesson 1 — Keep single source of truth**
When the same information appears in multiple docs (NFR table, QR version table), edits to one
do not propagate to others. Result: contradictions. Rule: pick one file as owner, other files link.

**Lesson 2 — Deprecated packages cause silent wrong paths**
`Microsoft.Windows.SDK.Contracts` is deprecated for .NET 5+. The correct approach is the Windows
TFM moniker (`net8.0-windows10.0.22000.0`). Always verify package recommendations against the
current .NET version before documenting them.

**Lesson 3 — Code skeletons must match the current spec**
The `QrCodeService` skeleton used `pixelsPerModule` as a parameter even after the spec changed to
`TargetSizePx`. Skeletons that diverge from the spec mislead implementers. After any spec change,
immediately update any skeleton that references the changed concept.

**Lesson 4 — Removed features must be purged from every file**
Removing Copy/Save buttons from the feature list did not remove them from `ARCHITECTURE.md`'s
`OverlayWindow` component description. Checklist: after removing a feature, grep all docs for
its name before committing.

**Lesson 12 — SendInput Ctrl+C on hotkey is fragile; just read the clipboard**
Synthesising Ctrl+C via SendInput has too many failure modes: modifier keys left held from
the hotkey combo contaminate the injected keystrokes, different apps handle Ctrl+C differently,
clipboard polling adds latency and race conditions. The correct solution is to let the user
copy text themselves (Ctrl+C) and then press the hotkey to read the clipboard directly.
This is zero-latency, zero-injection, and works in every application.

**Lesson 11 — Ctrl+Shift+Q conflicts with Firefox (close all tabs)**
Ctrl+Shift+Q is claimed by Firefox as "close all tabs". RegisterHotKey may silently fail
or the browser intercepts the key before our hook. Use F-key combinations as default
hotkey — Ctrl+Shift+F2 is safe across all common browsers and Windows itself.

**Lesson 10 — Hotkey modifiers contaminate the SendInput Ctrl+C**
When the hotkey fires (e.g. Ctrl+Shift+Q), the modifier keys are still physically held.
SendInput then produces Ctrl+Shift+C instead of Ctrl+C — in Chrome this opens DevTools,
not copy. Fix: use GetAsyncKeyState to detect held modifiers (Shift, Alt, Win) and inject
key-up events for them before the Ctrl+C sequence. Do not re-press them; the physical key
release will restore state naturally.

**Lesson 9 — Ära lisa taustatriggereid ilma eksplitsiitse nõudeta**
Global mouse hook (double-click capture) lisati eeldusena, et kasutaja tahab seda.
Tegelikult tahtis kasutaja ainult hotkey-käivitust. Reegel: ära lisa taustamonitoringut
ega automaatset käivitamist (hookid, timers, watchers) ilma selgesõnalise nõudeta.
App peab olema täiesti passiivne kuni kasutaja seda hotkey'ga käivitab.

**Lesson 8 — Don't auto-fallback to OCR; trust the user's selection**
The original hotkey pipeline fell back to OcrService.RecognizeCursorRegionAsync() when the
clipboard was empty. This captured a large 600×400 region which is never what the user wanted.
Rule: the hotkey (and double-click) should only use what the user has explicitly selected.
OCR is only available as a manual action via the overlay button.

**Lesson 7 — Clipboard.Clear() / all clipboard calls can throw CLIPBRD_E_CANT_OPEN**
`Clipboard.Clear()` (and all WPF Clipboard methods) throw `COMException 0x800401D0` when another
process holds the clipboard open. The existing retry only covered the poll loop; wrap every
clipboard entry point in a retry helper (8 × 25 ms back-off). Also add a top-level catch in the
pipeline caller so a clipboard failure degrades gracefully instead of crashing the app.

**Lesson 6 — Always commit and push without being asked**
After completing any task or set of changes, always run `git add`, `git commit`, and `git push` autonomously.
Never wait for the user to request it. If a PR doesn't exist yet, create one as a draft.

**Lesson 5 — Windows target must be consistent everywhere**
"Windows 11 target" was stated in NFRs while "Windows 10 1903+" appeared in Prerequisites and
other tables. Contradictions in OS requirements cause wrong build configurations. Define the OS
target once in NFRs and reference it everywhere else.
