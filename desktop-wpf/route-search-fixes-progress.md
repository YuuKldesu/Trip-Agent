# Window / search / transport execution ledger
- Base141cacc; branch codex/route-search-window-fixes-20261029; remote old-version backup backup/pre-route-search-fix-20261029-141cacc and local Git bundle.
- Clear user authorization: visible window border; automatic schedule with travel; working search; separate traffic card explicitly chosen.
- Ruling: remain in existing checkout on new feature branch, matching established workflow. Cost: same directory cannot safely switch branches during builds.
- Preflight: task1 search feeds task3 selected places and invalidation; task2 plan snapshots/receipt/projection feed task3 preview and confirmation. Use existing route tables, no schema migration or duplicate activity records.
- Root causes: AppChrome frame has no border/caption color separation; review has no preview/field backfill, routing runs only in final Generate; search handlers provide no busy or empty feedback and results are hidden in generic dropdown. Pointer-specific failure not yet established; controlled native search checks will distinguish actual handler/transport/UI results.
- Task1: RED missing outline / missing visible search control. GREEN 2 native frame checks +6 real HTTP/Amap search UI checks, core106/106. Search now exposes loading, address candidates, empty/error/retry; query changes clear selections. No live requests.
