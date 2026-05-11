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

**Lesson 6 — Always commit and push without being asked**
After completing any task or set of changes, always run `git add`, `git commit`, and `git push` autonomously.
Never wait for the user to request it. If a PR doesn't exist yet, create one as a draft.

**Lesson 5 — Windows target must be consistent everywhere**
"Windows 11 target" was stated in NFRs while "Windows 10 1903+" appeared in Prerequisites and
other tables. Contradictions in OS requirements cause wrong build configurations. Define the OS
target once in NFRs and reference it everywhere else.
