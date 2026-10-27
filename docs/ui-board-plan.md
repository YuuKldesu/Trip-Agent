# 行程看板 Implementation Plan

> Use superpowers:executing-plans inline, with one fresh final read-only reviewer. User has explicitly selected the reference UI and requested a new branch.

Goal: replace only WPF center board, retaining existing features; match app.py month/list look.
Spec: ui-board-integration.md. Branch: codex/ui-board-integration. WPF base: delivered 1.0.

## Review focus
- Cross-midnight and moved recurrence exceptions keep their original identity during month/list edits.
- Hidden active tables still affect conflicts; disabled visible cards stay editable.
- Six-row months, very long notes and many events grow/scroll rather than silently disappear.
- Completion marks never alter or cancel the schedule; failure to save marks does not fake success.
- Only the center board changes, Python and AI code untouched, no real user Data included in commits/packages.

## Task 1 — projection and UI preferences
- [x] BoardTests: known calendar grids (Oct2026, Feb2021/2024, Aug2020), overnight slices, moved exception, table filtering and move/reminder preservation; run RED then implement BoardProjection.
- [x] Test actual preference file roundtrip/corruption fallback, then implement BoardPreferences.

## Task 2 — WPF board
- [x] Add BoardStyles.xaml and ScheduleBoard.cs with month/list/week switching, navigation, auto rows, cards, completion, context actions and month drag/drop.
- [x] Bind existing editor/delete/select services, preserve weekly drag/resize; change only MainWindow center.
- [x] Run original and new tests, build, then actual WPF checks and rendered month/list/week/dense cases.

## Task 3 — handoff
- [x] One fresh review; important fixes reproduce RED→GREEN.
- [x] Publish distinct board-integration package, verify launch and screenshots; retain source on new branch with coherent docs.
- [x] Check Python/AI files unchanged and no secrets; do not merge main or push implementation without explicit instruction.

Progress: desktop-wpf/board-progress.md. No new NuGet dependency.
