# Guided Planner Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Deliver the approved native desktop navigation, date/time wheels, cross-day display and real conversational planning with branching and scoped undo.

**Architecture:** Extend the existing WPF application rather than replace its planner. Pure core components implement datetime arithmetic, conversation state and batch ownership; Data handles atomic local conversation persistence and model transport. Desktop composes the approved controls and reuses the existing location/route/conflict review before database writes.

**Tech Stack:** Existing .NET 10, WPF, Microsoft.Data.Sqlite 10.0.5, WebView2 1.0.4314.35. No additional package required.

**Spec:** docs/superpowers/specs/2026-10-28-guided-planner-design.md

## Global Constraints
- Warm rounded UI; original month styling and all activity data remain.
- 12 default table colors; 12—22 font range; today plus six days in compact mode.
- Duration 1—10080 minutes, reminders 0—10080 minutes, max 15 generated requests per batch.
- Model output never writes DB directly. No default database upload, no DSH delegation, no live paid calls for testing.
- Feature branch only; do not merge main as part of this task.

## Review Focus
- Month/year/leap transitions and reverse wheel carry update the date, not just the clock; confirmed changes preserve start duration without corrupting nullable automatic-start fields.
- Cross-day weekly exceptions and day fragments retain the same occurrence key, never double-count all-day totals or create a phantom midnight fragment.
- Editing older messages during a pending request or review cannot attach a stale reply or batch to the new branch.
- Undo after restart preserves unrelated inserts and refuses manually changed or newly route-linked generated records.
- Large fonts, minimum-width windows, dialogs and open panels must leave input, color selection, cancellation and confirmation reachable.

## Task 1: Date/time arithmetic and native pickers
**Files:** Create `Planner.Core/DateTimeSelection.cs`, `Planner.Desktop/DateTimePickers.cs`; modify `TimeEntry.cs`, `EditorWindow.cs`, `BoardProjection.cs`, `ScheduleBoard.cs`; add `Planner.Tests/GuidedPlannerTests.cs`.
**Interfaces:** `DateTimeSelection.Roll(DateTime value, bool hours, int steps): DateTime`; `DateTimeSelection.MoveStart(DateTime start,DateTime end,DateTime next): (DateTime Start,DateTime End)`; `StyledDateEntry.SelectedDate/SelectedDateChanged`; `TimeEntry.DateContext`, `DateTimeChanged` retain existing `Text`/`Changed` callers.
- [x] Add behavior tests for minute/hour carry both ways over leap day, unchanged duration, zero-midnight day segmentation and multi-day total/day duration.
- [x] Build and run tests; record expected missing-behavior failure before implementation.
- [x] Implement pure arithmetic and reusable popup controls; integrate full start/end dates and linking in activity editor without removing fields.
- [x] Verify tests and native picker interaction, cancellation, style and window bounds; commit.

## Task 2: Guided conversation, model protocol and safe generated batches
**Files:** Create `Planner.Core/GuidedConversation.cs`, `GeneratedBatch.cs`, `Planner.Data/ConversationStore.cs`, `GuidedAgentClient.cs`; modify `RepositoryAgentClient.cs`/`RepositoryPlanner.cs` only where needed for shared transport and explicit dated requests; add behavioral tests.
**Interfaces:** `GuidedConversation` stores `Branches`, `ActiveBranchId`; `ConversationBranch.Messages`, `Reply`; `ForkAt(messageId,text)` preserves history; `GuidedAgentClient.ReplyAsync(config,messages,token)` returns `GuidedReply(Message,Summary,Missing,Events,Ready)`; batch receipt records owned IDs/snapshots, `GeneratedBatchService.Undo(state,receipt)` preserves other changes.
- [x] Test branch prefix/old history isolation, protocol missing/invalid/ready responses, local incremental prompts, cancellation and request-context isolation with a real HttpMessageHandler fixture.
- [x] Test persisted conversation roundtrip and batch-scoped undo, restart, manual edits and linked-route rejection using real core and SQLite states.
- [x] Implement fixed prompt, bounded parsing, actual context-bearing service requests and local missing-field guide; atomic JSON conversation history file and generation receipts.
- [x] Keep existing route/revision checks and verify the whole domain suite; commit.

## Task 3: Native shell, settings, tables and cards
**Files:** Modify `MainWindow.xaml.cs`, `AssistantPanel.cs`, `BoardPreferences.cs`, `App.xaml`, `SwipeActivityCard.cs`, `PresentationPolicy.cs`, `ScheduleBoard.cs`; create `SettingsWindow.cs`, `ShellIcons.cs`, `TablePalette.cs`; update obsolete gesture expectations in tests.
**Interfaces:** Existing MainWindow editor/maps hooks and `PlannerViewModel` retained. Preferences add local profile and sidebar/panel state without DB schema changes. `SwipeOutcome` distinguishes left completion and right collection; old `Important` persistence keys migrate as collection without data loss.
- [x] Add behavior checks for both gestures, independent state, hidden uncollected star, palette selection without overwriting custom colors and cross-day synchronized state.
- [x] Build sidebar with Ctrl+B, optional left table panel and right assistant, operational board header, no duplicate font/layout/service controls on main.
- [x] Implement centered searchable settings categories, local avatar/nickname, existing service configuration and global control styling.
- [x] Integrate 12-color table editor, color identity on full-information cards and time-column list/compact agenda; preserve existing filters/maps/manual workflow.
- [x] Run domain and native shell checks at normal/narrow widths and 22px fonts; commit.

## Task 4: Real chat UI and checked generation/undo integration
**Files:** Create `Planner.Desktop/GuidedAssistantPanel.cs`; modify `AgentReviewWindow.cs`, `AssistantPanel.cs`, `PlannerViewModel.cs`; extend native checks.
**Interfaces:** Persistent panel binds the conversation service from task 2; `AgentReviewWindow` reports successfully persisted batch to caller; callback receives before/after states and owned activity/table IDs. No per-frame chat reconstruction on ordinary VM refresh.
- [x] Write native/HTTP fixture behavior checks for multi-turn answers, branch edits during pending requests, full context requests and no premature persistence.
- [x] Add editable user messages, branch switching, cancellation, ready summary, editable review, choose/new table with palette and independent pending request version token.
- [x] Capture receipt atomically with generation intent, apply through the existing review/route pipeline, show scoped undo and persist conversation/batch state. Handle restart recovery and modified batches explicitly.
- [x] Verify generation cancellation, conflicts, unrelated changes, repeated confirmation prevention and undo; commit.

## Task 5: Native regression, review and runnable delivery
**Files:** Update `Planner.BoardChecks`, `README.md`, `desktop-wpf/README.md`, `VERIFICATION.md`, build output and progress ledger.
- [x] Implement user-added native borderless `AppChrome` for application windows and verify caption controls, usable work area and resizing.
- [x] Run full domain suite and real WPF interaction suite; inspect screenshots of shell, settings, timeline, both picker popups, dialogs and compact 320px/22px font.
- [x] Publish self-contained Windows executable to a new output directory, retain all old packages/private data, extract archive into a clean directory and smoke-start independently.
- [x] Have a fresh reviewer inspect the entire branch against spec and ledger; fix important findings with reproducing behavior tests, run suites once more.
- [x] Commit documentation and verification, push development branch, report runnable paths, backup branch, counts and online/hardware limits. Do not merge main.

## Execution record
Progress and evidence: `desktop-wpf/guided-planner-progress.md`. User has authorized implementation in this chat; proceed continuously without another plan approval.
