# Ledger plan docs/superpowers/plans/2026-10-28-soft-ui-sidebar.md

User approved visual design and requested execution without further questions. Base69d1921, remote backup succeeded before product edits. Branch codex/soft-ui-sidebar-20261028.
Preflight: shared VM preferences feed board/main/settings; appearance only, no SQLite migration/AI/Python changes. Same projected recurrence keys in full/list/agenda. Body font dynamic resources must include controls with explicit original sizes.
Decision: default14pt-like WPF units, compacttoday+6, fade0.55, manual modes plus auto hysteresis; preview example states not imported. Scope of old drag-to-reschedule replaced by editor to avoid gesture collision, as approved discussion.
Tasks1-4 pending.

Task1 complete81/81 domain, prior73 retained. Preference additions and policies observed RED (missing APIs) then GREEN. One VM update入口 reads latest JSON.
Task2+3 implemented: removed old drag-to-reschedule, card completion checkbox/strike/color; added independent star/right-swipe opacity, warm date headers/rounded surfaces, agenda, dynamic fonts, mode controls/appearance/pin/menu. ActualWPF first pass57 assertions passed; source helper coordinate coverage being added.
Visual QA: original VisualBrush capture included invisible full-layout extents and squashed compact screenshot; direct RenderTargetBitmap confirms real400/320 compact layout readable, new captures use direct rendering. No product workaround for capture artifact.
Decision: left table controls wrap to avoid large-font clipping. Touch handlers implemented; real touch hardware not yet verified.

Final read-only review:1Important(table dialog at22 clips Delete),1Minor(full dimensions not restored). Regraded dimension loss Important because closing overwrites saved user UI size. Both enter single fix pass. Table viewport measurement initially compared against whole Window470 and falsely passed; clientBorder431 confirms Delete bottom463.8 clipped and RED observed. Fullstartup1260x760 regression RED observed separately. Fixes: scrollable table dialog and startup restore bounds clamped to work area.
Computer-use boundary: sky UIA matched isolated QA window, screenshot did not reliably match target. No mouse swipe injection attempted; closed verified QA close button and process exit0. Native mouse/touch hardware not claimed verified; actualWPF control/geometry checks remain evidence.

Tasks1-3 complete; final81/81 domain61/61 actualWPF,0warnings/errors. One review fix pass complete: table clipping RED→GREEN(client431, bottom463.8→406), full remembered1260x760 RED→GREEN. No deferred minors. Native hardware boundary recorded in VERIFICATION. Task4 packaging in progress.
