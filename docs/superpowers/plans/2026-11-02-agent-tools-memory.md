# Agent Tools and Memory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 实现对话内地图查询、只读日程工具和可追溯分支记忆。
**Architecture:** Core定义增量记忆及快照；Data负责受限工具和模型循环；Desktop提供候选选择、进度和草稿地点交接。
**Tech Stack:** C# / .NET 10 / WPF / existing Amap and Chat Completions clients.
**Spec:** docs/superpowers/specs/2026-11-02-agent-tools-memory.md

## Global Constraints
- 用户已确认方案，原地功能分支工作，不推送/合并main；已有自定义API变更单独提交备份。
- 全部工具只读，最终保存和撤回沿用原流程；密钥本地保存。
- 无新依赖、不调用真实收费接口、保留本地模式。

## Review Focus
- 较早消息改写不能携带未来记忆和地点选择；重启保留快照。
- 模型不能伪造用户确认、地点坐标、工具返回或无限查询。
- 查询失败/取消/旧回包不会写数据库或覆盖另一分支。
- 变更地点/时间使旧路线失效，已选地点正确传入核对页。
- 相关日期读取处理跨日和周期例外，不能漏出无关备注/禁用表。

### Task 1: Branch memory and provenance
**Files:** Core/AgentMemory.cs, Core/GuidedConversation.cs, Tests/AgentToolsTests.cs (all under desktop-wpf/Planner.*).
**Interfaces:** AgentMemory.Apply(updates,messages), snapshots on ConversationMessage; GuidedReply.Memory; TripRequest.PlaceRef.
- [x] Add tests for provenance, override, suggestion isolation, old-prefix fork and persistence; run RED.
- [x] Implement bounded typed facts, selected/candidate places, route snapshots, per-message memory restore; run GREEN and commit.

### Task 2: Read-only tools and model loop
**Files:** Data/AgentToolSession.cs, Data/GuidedAgentClient.cs, Data/AgentPrompt.cs, Tests/AgentToolsTests.cs.
**Interfaces:** ReplyWithToolsAsync(config,messages,memory,state,progress,token); tools search_places, query_route, read_schedule; tool result includes source/time.
- [x] Add HTTP fixtures proving tool result reinjection, ambiguity/no fabricated coordinates, bounds, cancellation, errors, date privacy and update validation; run RED.
- [x] Implement JSON action loop using same API, structured prompt and host validation; run GREEN and commit.

### Task 3: Desktop integration and runnable delivery
**Files:** Desktop/GuidedAssistantPanel.cs, Desktop/AgentReviewWindow.cs, Desktop/PlaceSearchBox.cs, BoardChecks/AgentToolChecks.cs, README.md.
**Interfaces:** candidate buttons call ConfirmPlaceAsync; review receives host-selected places via optional dictionary; requests with PlaceRef validated against selected place.
- [x] Add native flow test for search→user select→route→ready→review preserving coordinates and stale branch isolation; run RED.
- [x] Wire context, query progress and memory persistence, 120s deadline, candidate UI, bounded memory inspector and draft place reuse; run GREEN.
- [ ] Full build/core tests, focused native tests, package/start check; record known baseline native failure separately.
- [ ] Fresh readonly final review, address important findings with RED/GREEN, record verification and commit. No automatic push.
