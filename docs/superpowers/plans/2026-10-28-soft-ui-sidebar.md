# Soft UI and Sidebar Implementation Plan

> Execute inline via superpowers:executing-plans; one fresh final read-only reviewer.

Goal: approved soft calendar, independent stars/right swipe completion, manual fonts, responsive full/agenda layouts.
Spec: ../specs/2026-10-28-soft-ui-sidebar-design.md
Tech: existing C#/WPF/SQLite, no extra packages.

## Review focus
- Stale preference snapshots must not overwrite completion/star/font/mode fields; old JSON migrates safely.
- Short/left/vertical/cancelled swipes and star/button clicks never toggle completion or trigger edit/add; right swipe toggles exactly once.
- Resizing mode switches/fonts retain selection, independent scroll and open editor/AI input; threshold hysteresis prevents oscillation.
- Large fonts and320wide compact header/agenda remain usable; helpers/dialogs stay reachable; full forced mode handles minimum width.
- Completion/star keys keep original recurrence identity and do not change schedule revision/constraints/reminders.

## Task1 preferences/policies
- Add SoftUiTests: old prefs, independent important/completed, fresh read update, invalid font/mode, right-right toggling policy, rejected gestures, adaptive hysteresis/manual override/font thresholds. Watch RED.
- Implement Core PresentationPolicy+SwipeTracker, Data preference extensions/update, VM shared appearance entry, Typography dynamic font resources. Green full suite.

## Task2 calendar/card/agenda
- Implement SwipeActivityCard real mouse/touch handlers, opacity animation, independent vector star, context actions; remove old month drag and completion checkbox.
- Round board/day/card/toolbar, warm red date heads+gray content+white cards. Reuse month/list services and cache, agenda next7days using same occurrence cards/actions.
- ActualWPF checks no completion checkbox, star vs completion, gesture integration, shape/header, fontsize and prefs persistence.

## Task3 shell and settings
- Responsive MainWindow retains control instances and VM; manual auto/full/compact selection, appearance window font presets/slider/reset, compact pin/menu/table manager.
- ActualWPF checks320/400wide compact, repeated threshold resize, full return, large font, editor availability, database unchanged by UI updates; screenshots.

## Task4 handoff
- Whole build/tests and UI checks, one fresh static review and one RED→GREEN important fix pass.
- New selfcontained runtime, smoke extracted EXE, source extracted build, archive SHA checks, no userData/secrets; commit on feature branch, no main merge or implementation push.
