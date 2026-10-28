# Feature UI Integration Implementation Plan

> Execute inline with superpowers:executing-plans; one fresh read-only final reviewer.

Goal: port feature/ui978c678 logic/UI into WPF, warm local style, four explicit requirements.
Spec: ../specs/2026-10-28-feature-ui-integration-design.md
Global constraints: new feature branch, main untouched, backup remote done first, no real secrets/Data committed, no external AI invocation.

## Review focus
- First show/resize/cache switch must fill calendar rows without click; notes may grow rows.
- Card or completion input must not bubble into blank-day add; genuinely empty area must add correct date.
- Cached views and filters invalidate after edits/marks; independent scroll survives switching.
- Route/LLM async work must not commit stale revisions, failed routes or cancelled/closed editors.
- Seed data only fresh empty store, durable user edits preserved; recurring identity retained.

## Task 1 core and preferences
- Add failing tests for WEEK migration, completed filtering, uneven/idempotent sample data, repository earliest-gap/explicit conflicts/batch order/15 cap/return marker.
- Implement Core RepositoryPlanner, DemoSchedule, BoardLayout and preference filtering; red then green full suite.

## Task 2 board/editor
- ActualWPF regression checks first for no week/no plus/blank add/first fill/resize/filter/cache/editor time linkage; run RED.
- Replace ScheduleBoard layout lifecycle/cache/input behavior, remove CalendarView; port compact nonmodal EditorWindow and pickers with route checking.

## Task 3 repo agent
- Port extraction payload/local parser/provider handling to RepositoryAgentClient and existing map service adapter; bounded async responses/cancellation.
- Replace old limited assistant with extraction->editable review->route selection->preview->atomic save, preserve encrypted service configuration.
- Tests use fixtures only, no paid external calls.

## Task 4 verification/handoff
- Whole tests/build and actualWPF screenshots; one fresh reviewer, important findings reproduce RED then fix in one pass.
- Separate selfcontained publish, extracted EXE smoke, source/runtime hash checks and local commit; remote backup remains untouched, no main merge.
