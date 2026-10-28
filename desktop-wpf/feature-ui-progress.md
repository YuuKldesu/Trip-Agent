# Ledger plan docs/superpowers/plans/2026-10-28-feature-ui-integration.md

Backup first: remote backup/local-wpf-20261028-90c1f61 verified push succeeded. Feature branch codex/feature-ui-logic-20261028.
Reference: origin/feature/ui978c678, downloaded four core files match bytes after line ending normalization.
Preflight: repository extraction/greedy scheduling adapts to existing ChangeService/SQLite, never a second store. Core projection identity remains. UI events delegate to editor, and completion stays independent.
Decision: existing WPF platform retained, repository interactions/logic ported; Python history merged separately. Demo only genuinely fresh empty database to protect user edits.
Tasks1-3 implemented. Core observed RED missing PreparedTrip then66/66 GREEN. Additional return-marker and outgoing-departure regressions observed66/68 RED then68/68 GREEN. Cancellation69/70 RED, fix pending final full run. ActualWPF first observed week-removal RED, then first-fill/blank-add/cache/editor/demo checks GREEN; latest UI33 assertions counted from log at handoff.
Decision: retain optional unverified manual activities for everyday locations and cross-midnight schedules; strict selected traffic verification rejects failed routes. Automatic repository planning always requires selected places and 08-22 first feasible gaps.
Decision: existing schema stores positive durations; return-arrival marker reserves1 minute and is labeled, avoiding collisions in persisted schedules. Prior21st-century schedule features remain; no Data migrations.
Task4 pending fresh final read-only review and package verification.

Tasks1-3 complete: final73/73 domain,36/36 actualWPF, Release0warnings/errors. Cancellation RED→GREEN and offline full UI-save pipelines verified.
Final review: 2Important, 0Critical,1Minor regradedImportant (user duration silently ignored has scheduling consequences). All3 fixed in one pass; nested-overlap domain70/71 RED, actualWPF duration+newweekly RED, new future-conflict API missing RED, final73/73+36/36 GREEN.
Final review limits: static reviewer did not run services/user data; implementer verified actual控件 persistence and screenshots. No deferred minors. Packaging in progress.
