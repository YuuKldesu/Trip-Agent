# SDD ledger — plan: docs/superpowers/plans/2026-10-27-desktop-implementation.md

用户已批准设计并授权开始开发；采用当前对话内执行。

环境：SDK 10.0.201，WindowsDesktop reference pack 10.0.5。dotnet --info 的工作负载枚举被系统权限阻止，已用 --list-sdks 与实际组件目录确认基础工具。
隔离：当前目录非Git仓库，源码位于独立 software 子目录；不创建无意义的仓库或修改课程材料。
NuGet：普通执行无法连接本地代理，元数据查询通过受控提权成功；依赖保存在工作区内。
Pre-flight: 任务1提供领域模型与服务；任务2保存相同模型；任务3调用任务1/2；任务4/5复用相同事务提交；任务6覆盖全部。无接口冲突。

Task 1: in progress — 先建立可运行测试基线，再实现领域规则。

Task 1: complete — 11/11 domain tests RED→GREEN. Task 2: complete — 14/14 including SQLite roundtrip, FK rollback, stale revision and future exception preservation. Task 3: core interface complete — WPF build 0 warnings; actual WPF smoke rendered 1480x920 and persisted sample data in qa/smoke-initial. Reminder: build with --disable-build-servers -m:1 -p:UseSharedCompilation=false; normal multi-node build fails without diagnostics under restricted process IPC. Tasks 4/5/6 in progress.

Tasks 4/5 core complete: 31/31 behavior tests green. Added real Amap HTTP/parser, Nanjing administrative geofence including concave segment crossings, finite beam planner with vehicle location tracking, atomic selected-plan save, DPAPI settings, scoped LLM draft preview and cancellation, runtime/missed reminder dedup. Desktop 0 warnings/errors; final WPF render inspected at qa/smoke-final/ui-smoke.png. Live APIs remain unverified without user credentials. Route UI smoke and final review/package still pending.

Tasks 4/5/6 implementation complete pending final verification: 33/33 tests, per-leg route alternatives with whole-plan resource/time validation, unavailable mode isolation, startup missed-reminder summary. Ruling: automatic scheduling has a 12-item / 100-query finite-search budget and refuses cross-day automatic scheduling; manual cross-day editing is supported — bounded interactive behavior avoids unlimited paid requests, at the cost of requiring manual planning for larger/cross-day cases. Ruling: no user credentials were supplied; implement service adapters and fixture/HTTP validation, retain honest configuration prompts — live provider compatibility remains a user-configuration validation step. Native UI added and saved a test item, app closed for restart check. No Git range: dedicated software folder is the complete review scope.

Native UI evidence: Computer Use launched actual NanjingPlanner.exe; opened Add window, entered 手动保存验证（测试）, clicked Save, observed 09:00–10:00 card; closed process and re-launched, same card restored. Opened route window and visually inspected full settings/form plus honest missing-map-config prompt, no network/credentials used. Startup executable works. Warm main screenshot is qa/smoke-final/ui-smoke.png; UI temporary test data stays in debug Data only, excluded from distribution.

Final review: read-only whole-project review by fresh software_final_review; 7 Important and 2 originally Minor findings. Re-graded both minors Important: inability to replace invalid protected keys blocks service configuration; lost fare snapshots breaks saved-plan review. All 9 fixed in one correction pass with reproducing regression checks; suite 47/47 Release green (qa/tests-release-final.txt). Additional tests cover single-instance cross-table rejection and authorized inactive-table context. No deferred minor findings.
Final: fixed route/date/resource-chain invalidation, batch weekly version guard, future exception reminder/delivery/endpoint migration, concrete future-rule preview, visible inactive tables, transit access-walk consistency and single-override window policy — review-red-full.txt → review-green.txt RED→GREEN; final full suite 47/47.
Final: fixed key recovery and persisted total fare; schema v1→v2 actual SQLite migration tested. DPAPI saved-key roundtrip tested on Windows.
Final: Ruling: reviewer declined live service judgment — no credentials were supplied, so real provider permissions/compatibility/maps/routes remain unverified, with explicit prompts and no fabricated data; cost is a later configuration and live integration step. Earlier ledger wording 'fixture/HTTP validation' means adapter/schema preparation only; no end-to-end HTTP service verification was performed.
Final: Ruling: reviewer declined build/UI/package judgment as read-only — primary executor ran Release build (0 warnings/errors), 47 behavioral checks, native save/restart and packaged EXE smoke (exit 0), with main/editor/route renders visually inspected; cost is that other machines and untested UI combinations still need user testing.
Final: Ruling: reviewer declined real candidate-quality judgment — retain declared finite search bounds and no global-optimality claim; cost is that real travel quality requires configured services and user trial.
Tasks 1–6 complete for local implementation and package preparation. Final package integrity verification and artifact handoff follow; live service integration is explicitly not claimed.

Final delivery checks: packaged ZIP extracted and its self-contained EXE launched with exit 0; main/editor/routes rendered. Source ZIP extracted independently, restore with single-process build-server flags, rebuilt Release and passed 47/47. Updated build.ps1 to use the same restore flags and preserve caller directory; full script then ran successfully, including tests and publish. Archived payloads verified against originals by SHA256 (469 files); final docs and script refresh included before final checksum regeneration. Local output/Data observed and preserved; excluded from ZIP. No credentials or test DB included in distribution.
