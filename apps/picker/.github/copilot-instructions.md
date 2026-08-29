---
applyTo: '**'
---
# Modern Emoji Picker agent context

The repository-root `AGENTS.md` governs language, issue tracking and domain documentation. Read the relevant source before changing behaviour:

- Product behaviour: `docs/specs/01-modern-emoji-picker.md`
- Work and blockers: `.scratch/modern-emoji-picker/issues/`
- Domain language: `CONTEXT.md`
- Decisions: `docs/adr/`
- Qualification: `docs/qualification/`

## Invariants

- Target .NET 10 WPF on `win-x64`; build from `ModernEmojiPanel.sln`.
- Runtime emoji data and Noto artwork are pinned repository assets. Keep ordinary build, test and runtime offline.
- Modern identity and `%APPDATA%\ModernEmojiPicker` remain isolated from Classic. Classic conflict detection is read-only.
- Insert only into the captured pre-picker target after immediate foreground/integrity validation. Abort without retry or retarget.
- Preserve clipboard formats during Temporary Paste. Explicit Copy is the user-intended clipboard-history action.
- Preserve bounded insertion/cache behaviour, UI virtualisation and qualification budgets.
- Human-facing documentation is Thai. Agent-only instructions may be English. Code comments use Australian English.

## Completion

For a ticket checkpoint, update acceptance evidence, run the relevant `scripts/verify-*.ps1` checks, and run `scripts/test-clean-checkout.ps1 -Revision HEAD` before integration when the change crosses product boundaries.

Ticket 14 defines the local-only self-contained packaging route. Ticket 15 owns Draft/public release after Ticket 13; public publish requires explicit user intent.
