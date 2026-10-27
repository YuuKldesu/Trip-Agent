# SDD ledger — plan: docs/ui-board-plan.md

User selected screenshot2 month/list as replacement for screenshot1 center, existing features retained, AI deferred to collaborator. Explicit new branch instruction followed.
Backup complete: workspace/backups/2026-10-27_pre-board_13e6c3c9/workspace-before-ui.zip (911 files, SHA256 verified, includes actual local Data; kept outside repo).
Pre-flight: BoardProjection reuses CalendarEngine display expansion; board selection/editor/delete delegate to unchanged existing services; completion lives in UI preferences, no SQL migration. Existing CalendarView retained for time drag/resize. Python files untouched.
Ruling: use delivered WPF1.0 source as screenshot-matching UI baseline; unrelated unfinished conversation work is backed up separately. Cost: conversational integration remains a later user-directed task.
Task 1: complete — initial RED then 56/56 GREEN, original47 retained; preference RED then GREEN.
Task 2: complete — actual WPF checks15/15; screenshots exposed narrow-window layout issue, promoted to final review fix.
Final review: fresh read-only reviewer board_final_review found3 Important, no Critical or Minor. Scope comparison confirms Python and original AI/service source unchanged.
Final: fixed nonuniform month columns/header alignment, stale empty-list navigation and unsynchronized selected week — three actual WPF regression checks observed RED then GREEN; final WPF19/19, domain56/56; build0 warnings/errors. Published EXE smoke exit0.
Task 3: complete — separate self-contained publish, domain56/56 and actualWPF19/19, runtime426 entries SHA256 verified, extracted EXE smoke exit0. Branch retained locally, no main modification/merge/push.
Additional baseline check: unchanged Python suite26/28, two pre-existing Windows temporary SQLite cleanup handle errors; recorded in VERIFICATION, no unrelated Python fix.
Final review declines: real mouse drag/dialog-save combinations not independently operated; UI-event checks plus domain identity tests and unchanged editor services are the evidence. Live AI/maps explicitly deferred. Archive contents checked by implementer. Save-failure behavior inspected, disk failure not injected.
