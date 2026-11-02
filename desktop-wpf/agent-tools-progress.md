# Agent tools progress — plan: docs/superpowers/plans/2026-11-02-agent-tools-memory.md

Ruling: 用户已两次确认功能方案并授权开始，直接在本会话执行；不再要求选择执行方式。沿用现有功能分支，不创建额外工作树，避免移动当前已验证工程。
Ruling: 用 JSON tool_requests 实现工具循环，兼容已存在的自定义API/JSON响应模式；代价是提示词需要明确动作格式，不依赖服务商 function calling。
Pre-flight: Task1 snapshot memory is consumed by Task2 and Task3; use one typed AgentMemory DTO, only host executes/records tools and choices.
Baseline: d426422 + df5192c custom API; core132/132; native98 checks then known rounded-card assertion failure.

Task 1: complete — provenance/override, prefix restoration, restart/route invalidation tests RED (missing DTOs) → GREEN135/135. DateTime snapshot defaults must be valid China-local timestamps.

Task 2: tool protocol/loop implemented with host-selected place references, schedule reads without notes, exact departure route checks, 6 executions / 6 model calls cap, bounded result and cancellation. Added readiness gate RED to reject unchecked schedule/routes.
Task 2: complete — core140/140, no real services called. JSON assertions decode Unicode escapes rather than treating encoded Chinese as missing data.

Task 3 implementation: candidate buttons + branch/version guard, host selection, source-address review reuse, route snapshot cache, structured memory inspector, query progress, cancellation and 120s deadline.
Evidence: core145/145 (tmp/agent-final-core.log); build0 warnings/errors (tmp/agent-final-build.log); focused native15/15 (tmp/agent-tools-native-03/board-checks.txt), including save/traffic/undo, prefix edit, restart and cancelled old reply isolation. Map tool executes real AmapClient parsing via bounded HTTP fixtures, not external paid calls.
Ruling: stored places attached to already-saved relevant occurrences are trusted host endpoints and exposed as place_ref so existing-event transfers can be queried without asking the user to reselect an already saved location. Source status remains visible; cost if wrong is outdated stored coordinates, rechecked in review.
Ruling: retain prior draft as STALE on partial followups so memory is not erased; only current Ready reply can enable generation. Cost if wrong is model referring to an old suggestion; explicit prompt status and final software review prevent automatic saving.
