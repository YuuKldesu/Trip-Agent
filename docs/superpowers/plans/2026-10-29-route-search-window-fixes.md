# Window, search and generated travel fixes

> **For agentic workers:** Use superpowers:executing-plans inline. One fresh whole-branch review at the end; no implementer agents.

**Goal:** Distinct app window boundaries; actual visible place search; editable automatically scheduled times and standalone persisted transport cards.
**Architecture:** Keep WPF and existing planner/SQLite schema. Reuse Plan/PlanLeg/RouteSegment for travel snapshots, project read-only travel cards on board, and extend batch receipts to own these records. Draft preview calculates only; final confirmation persists atomically.
**Tech Stack:** Existing .NET10/WPF/SQLite; no new dependencies.
**Base:** 141cacc87ef4d30a3543e929871ed9355bc246a2. Main unchanged. User explicitly chose separate transport cards on2026-10-29.

## Constraints and decisions
- Retain native-borderless chrome, add visible warm outline and caption separation to main/dialog windows.
- For10:00—14:00 activity A and30min activity B: route from A departure14:00, arrive14:00+chosen route minutes; B begins on arrival and ends30min later. Never invent route values. Manual fixed times remain fixed; explicit conflicts refuse save.
- Place search shows loading, address-bearing selectable results, empty/error feedback and retry. Unique result may auto-select; multiple results require selection. Query changes invalidate old selections/responses.
- Auto preview and edits do not write DB. Existing revision/branch checks and confirm-once behavior remain. Cancel pending work on close.
- Travel snapshots are existing domain route records, not fake duplicate user activities. Local estimates clearly labelled UNVERIFIED; real geometry/cost retained when available.
- Generated route records are owned by the same undo batch; undo retains later unrelated changes and refuses modified/new external route dependencies.
- No live paid calls/DSH testing; controlled HTTP responses exercise real Amap request/parser. Read no private Data. Publish new output; keep previous packages.
- Use existing checkout on a new feature branch, matching established user workflow; no concurrent branch switching.

## Review Focus
- Automatic time fields must remain automatic when recalculating; manual edits must remain explicit. Changed query/mode/time cannot allow stale calculated plans to save.
- Saved transport cards preserve actual departure/arrival, map estimates/verification, cross-day projection and table visibility, without being counted as duplicate user activities or reserving travel twice.
- Undo removes owned route objects while refusing external route links or changed geometry/times; automatic STALE flags from unrelated edits must not block otherwise safe undo.
- Search zero/multiple/failed results visibly differ; older response cannot replace newer query or selection. Loading/search/compute controls remain reachable at22px/760x600.

## Task 1: Window boundaries and visible place search
- [ ] Add failing native checks for outline/caption separation and functional place search loading/results/empty/error/retry with actual Amap fixture transport.
- [ ] Add shared visible search control, keep keyword editing, auto unique choice and explicit multiple-choice selection, unify initial/manual search behavior.
- [ ] Add warm window outline/separate caption; run core/native relevant checks and commit.

## Task 2: Transport records and safe batch undo
- [ ] Add failing core integration tests for14:00+route+30min, saved SQLite routes/board fragments and scoped undo after restart/external edits.
- [ ] Preserve previous activity/occurrence/departure in plan rows and route snapshots; apply activities+routes in one state revision; project read-only traffic cards.
- [ ] Extend old-compatible batch receipt route ownership and validation; verify full core suite, commit.

## Task 3: Live draft scheduling and final delivery
- [ ] Add failing native two-activity fixture checks for auto time fill, route display, edits/recalculation, no draft writes, confirm persistence/reload/undo.
- [ ] Integrate debounced cancellable preview and explicit compute/route-choice button; invalidate on relevant changes and protect manual fields and stale replies.
- [ ] Run full core/native suites, inspect screenshots, one fresh code review and one tested fix pass if needed.
- [ ] Publish a new self-contained package, extract and smoke-start; source archive/checksums; push feature branch, main untouched.

Progress: desktop-wpf/route-search-fixes-progress.md.
