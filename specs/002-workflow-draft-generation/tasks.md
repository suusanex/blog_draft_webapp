# Tasks: ワークフロー型ブログ下書き生成（段階生成）

**Input**: Design documents from `/specs/002-workflow-draft-generation/`
**Prerequisites**: plan.md (✅), spec.md (✅), data-model.md (plan.md内に含む), contracts/ (plan.md内に含む)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US4, US2, US5, US3, US6)
- Include exact file paths in descriptions

## Path Conventions

プロジェクトはモジュラーモノリス構成（Blazor Server + ASP.NET Core API）:
- Core層: `src/BlogDraftWebApp.Core/`
- API層: `src/BlogDraftWebApp.Api/`
- UI層: `src/BlogDraftWebApp/Components/Pages/`
- テスト: `tests/BlogDraftWebApp.*.Tests/`

---

## Phase 1: Setup（プロジェクト初期化）

**Purpose**: LiteDB依存追加、.gitignore更新、永続化ディレクトリ作成

- [ ] T001 Add LiteDB NuGet package to BlogDraftWebApp.Core.csproj
- [x] T002 [P] Verify .gitignore excludes **/data/, *.db, *.db-shm, *.db-wal
- [ ] T003 [P] Create data/ directory in src/BlogDraftWebApp/ with .gitkeep

**Checkpoint**: 依存関係とプロジェクト構造が整備され、実装開始可能

---

## Phase 2: Foundational（ブロッキング前提条件）

**Purpose**: 全ユーザーストーリーが依存する共通インフラ（データモデル、永続化、オーケストレーション）

**⚠️ CRITICAL**: このフェーズが完了するまで、いかなるユーザーストーリー作業も開始できない

### データモデル

- [ ] T004 [P] Create WorkflowStep enum in src/BlogDraftWebApp.Core/Models/WorkflowStep.cs
- [ ] T005 [P] Create RagSnapshot model in src/BlogDraftWebApp.Core/Models/RagSnapshot.cs
- [ ] T006 [P] Create Outline model in src/BlogDraftWebApp.Core/Models/Outline.cs
- [ ] T007 [P] Create TitleHook model in src/BlogDraftWebApp.Core/Models/TitleHook.cs
- [ ] T008 Create WorkflowSession model with state transition methods in src/BlogDraftWebApp.Core/Models/WorkflowSession.cs (depends on T004-T007)

### 永続化（IWorkflowRepository）

- [ ] T009 Define IWorkflowRepository interface in src/BlogDraftWebApp.Core/Services/IWorkflowRepository.cs
- [ ] T010 Implement LiteDbWorkflowRepository in src/BlogDraftWebApp.Core/Services/LiteDbWorkflowRepository.cs (depends on T009, T008)

### オーケストレーション

- [ ] T011 Define IWorkflowOrchestrator interface in src/BlogDraftWebApp.Core/Services/IWorkflowOrchestrator.cs
- [ ] T012 Implement WorkflowOrchestrator with state transition logic in src/BlogDraftWebApp.Core/Services/WorkflowOrchestrator.cs (depends on T011, T010, T009, T008)

### プロンプト合成拡張

- [ ] T013 Extend PromptComposer to support step-specific instructions in src/BlogDraftWebApp.Core/Services/PromptComposer.cs

### 設定

- [ ] T014 [P] Create WorkflowOptions configuration model in src/BlogDraftWebApp.Core/Configuration/WorkflowOptions.cs
- [ ] T015 [P] Add workflow settings to appsettings.json (DatabasePath, SessionRetentionDays, CleanupSchedule)

### 例外

- [ ] T016 [P] Create InvalidStateTransitionException in src/BlogDraftWebApp.Core/Exceptions/InvalidStateTransitionException.cs
- [ ] T017 [P] Create SessionNotFoundException in src/BlogDraftWebApp.Core/Exceptions/SessionNotFoundException.cs

### 排他制御（同一セッションの同時操作防止）

- [ ] T099 [P] Add per-session lock utility (SemaphoreSlim dictionary) in src/BlogDraftWebApp.Core/Services/WorkflowSessionLock.cs
- [ ] T100 Apply per-session lock to mutating operations (generate/save/confirm/refresh) in src/BlogDraftWebApp.Core/Services/WorkflowOrchestrator.cs (depends on T099)
- [ ] T101 Map lock contention to HTTP 409 SESSION_BUSY in src/BlogDraftWebApp.Api/Endpoints/WorkflowEndpoints.cs and src/BlogDraftWebApp.Api/Middleware/GlobalExceptionHandler.cs (depends on T100)
- [ ] T102 [P] Add unit test to verify lock enforces single in-flight operation per session in tests/BlogDraftWebApp.Core.UnitTests/Services/WorkflowOrchestratorTests.cs (depends on T100)

**Checkpoint**: 基盤完成 - ユーザーストーリー実装が並行開始可能

---

## Phase 3: User Story 1 - アウトライン生成と確定 (Priority: P1) 🎯 MVP Part 1

**Goal**: ユーザーが記事概要を入力し、AIがアウトライン（章立て＋箇条書き）を生成し、編集・確定できる

**Independent Test**: セッション作成 → アウトライン生成 → 編集 → 確定が完了し、サーバーに保存される

### API実装

- [ ] T018 [P] [US1] Create CreateSessionRequest DTO in src/BlogDraftWebApp.Api/Models/CreateSessionRequest.cs
- [ ] T019 [P] [US1] Create CreateSessionResponse DTO in src/BlogDraftWebApp.Api/Models/CreateSessionResponse.cs
- [ ] T020 [P] [US1] Create GenerateStepRequest DTO in src/BlogDraftWebApp.Api/Models/GenerateStepRequest.cs
- [ ] T021 [P] [US1] Create GenerateStepResponse DTO in src/BlogDraftWebApp.Api/Models/GenerateStepResponse.cs
- [ ] T022 [P] [US1] Create SaveStepRequest DTO in src/BlogDraftWebApp.Api/Models/SaveStepRequest.cs
- [ ] T023 [P] [US1] Create ConfirmStepRequest DTO in src/BlogDraftWebApp.Api/Models/ConfirmStepRequest.cs
- [ ] T024 [US1] Implement POST /workflow/sessions endpoint in src/BlogDraftWebApp.Api/Endpoints/WorkflowEndpoints.cs (depends on T018, T019, T012)
- [ ] T025 [US1] Implement POST /workflow/sessions/{id}/steps/outline/generate endpoint (depends on T020, T021, T012, T013)
- [ ] T026 [US1] Implement POST /workflow/sessions/{id}/steps/outline/save endpoint (depends on T022, T012)
- [ ] T027 [US1] Implement POST /workflow/sessions/{id}/steps/outline/confirm endpoint (depends on T023, T012)

### UI実装

- [ ] T028 [P] [US1] Create WorkflowOutline.razor page in src/BlogDraftWebApp/Components/Pages/WorkflowOutline.razor
- [ ] T029 [US1] Implement outline generation UI with textarea editor in WorkflowOutline.razor (depends on T028, T024, T025)
- [ ] T030 [US1] Implement save and confirm buttons with API integration in WorkflowOutline.razor (depends on T029, T026, T027)
- [ ] T031 [P] [US1] Add navigation link "ワークフロー生成" to main menu

**Checkpoint**: User Story 1 完全動作 - アウトライン生成から確定まで独立してテスト可能

---

## Phase 4: User Story 4 - ワークフロー状態の保存と再開 (Priority: P1) 🎯 MVP Part 2

**Goal**: ページリロードやブラウザ閉鎖後も、WorkflowSessionの状態を復元して作業を再開できる

**Independent Test**: US1で作業途中にページリロード → 編集内容と現在ステップが復元される

### API実装

- [ ] T032 [P] [US4] Create GetSessionResponse DTO in src/BlogDraftWebApp.Api/Models/GetSessionResponse.cs
- [ ] T033 [US4] Implement GET /workflow/sessions/{id} endpoint in src/BlogDraftWebApp.Api/Endpoints/WorkflowEndpoints.cs (depends on T032, T012)
- [ ] T034 [US4] Update WorkflowOrchestrator to update LastAccessedAt on session retrieval in src/BlogDraftWebApp.Core/Services/WorkflowOrchestrator.cs

### UI実装

- [ ] T035 [US4] Implement session resume logic in WorkflowOutline.razor OnInitializedAsync (depends on T033, T028)
- [ ] T036 [US4] Add auto-save on content change in WorkflowOutline.razor (depends on T035, T026)

### セッション期限管理

- [ ] T037 [US4] Implement session expiration check in WorkflowOrchestrator.GetSessionAsync (throw SessionNotFoundException if expired) in src/BlogDraftWebApp.Core/Services/WorkflowOrchestrator.cs
- [ ] T038 [US4] Display "期限切れ" error message in UI when SessionNotFoundException occurs in WorkflowOutline.razor

**Checkpoint**: User Story 1 + 4 統合完了 - 永続化と再開が動作

---

## Phase 5: User Story 2 - 下書き生成と確定 (Priority: P2)

**Goal**: 確定アウトラインから、ブログ記事本文（下書き）を生成し、編集・確定できる

**Independent Test**: US1でアウトライン確定済み → 下書き生成 → 編集 → 確定が完了

### API実装

- [ ] T039 [US2] Implement POST /workflow/sessions/{id}/steps/draft/generate endpoint in src/BlogDraftWebApp.Api/Endpoints/WorkflowEndpoints.cs (depends on T025, reuses DTOs)
- [ ] T040 [US2] Implement POST /workflow/sessions/{id}/steps/draft/save endpoint (depends on T026, reuses DTOs)
- [ ] T041 [US2] Implement POST /workflow/sessions/{id}/steps/draft/confirm endpoint (depends on T027, reuses DTOs)
- [ ] T042 [US2] Add step order validation (reject draft generation if outline not confirmed) in WorkflowOrchestrator (depends on T012)

### UI実装

- [ ] T043 [P] [US2] Create WorkflowDraft.razor page in src/BlogDraftWebApp/Components/Pages/WorkflowDraft.razor
- [ ] T044 [US2] Implement draft generation UI with textarea editor in WorkflowDraft.razor (depends on T043, T039)
- [ ] T045 [US2] Implement save, confirm, and navigation to next step in WorkflowDraft.razor (depends on T044, T040, T041)
- [ ] T046 [US2] Add session resume logic in WorkflowDraft.razor OnInitializedAsync (depends on T045, T033)

**Checkpoint**: User Story 2 独立動作 - アウトライン→下書きの流れが完全機能

---

## Phase 5.5: RAG再検索（スナップショット更新と差分確認） (Priority: P2)

**Goal**: ユーザーが任意のタイミングでRAG再検索を行い、新旧スナップショット差分を確認し、採用を確定できる

### Core/API

- [ ] T112 [P] Add RagSnapshotDiff model in src/BlogDraftWebApp.Core/Models/RagSnapshotDiff.cs
- [ ] T113 Implement refresh flow in WorkflowOrchestrator (create new snapshot, compute diff, require confirm) in src/BlogDraftWebApp.Core/Services/WorkflowOrchestrator.cs (depends on T005, T112)
- [ ] T114 [P] Add DTOs RefreshRagSnapshotRequest/Response in src/BlogDraftWebApp.Api/Models/
- [ ] T115 Implement POST /workflow/sessions/{id}/rag/refresh and POST /workflow/sessions/{id}/rag/refresh/confirm endpoints in src/BlogDraftWebApp.Api/Endpoints/WorkflowEndpoints.cs (depends on T114, T113)

### UI

- [ ] T116 Add "RAG再検索" button and diff display + confirm (simple confirmation) to WorkflowOutline.razor in src/BlogDraftWebApp/Components/Pages/WorkflowOutline.razor (depends on T115)
- [ ] T117 Add "RAG再検索" button and diff display + confirm to WorkflowDraft.razor in src/BlogDraftWebApp/Components/Pages/WorkflowDraft.razor (depends on T115)
- [ ] T118 Add "RAG再検索" button and diff display + confirm to WorkflowTitle.razor in src/BlogDraftWebApp/Components/Pages/WorkflowTitle.razor (depends on T115)

### Tests

- [ ] T119 [P] Add integration tests for RAG refresh diff and confirm behavior in tests/BlogDraftWebApp.Api.IntegrationTests/WorkflowEndpointsTests.cs

**Checkpoint**: RAG再検索が独立してテスト可能

---

## Phase 6: User Story 5 - プレビューモード（プロンプト確認） (Priority: P2)

**Goal**: 各ステップで、LLM送信前にプロンプト全文をプレビュー表示できる

**Independent Test**: US1またはUS2で「プレビュー」選択 → プロンプト全文表示、警告表示、ログ記録なし

### API実装

- [ ] T047 [P] [US5] Create PreviewPromptResponse DTO in src/BlogDraftWebApp.Api/Models/PreviewPromptResponse.cs
- [ ] T048 [US5] Implement POST /workflow/sessions/{id}/steps/outline/preview endpoint in src/BlogDraftWebApp.Api/Endpoints/WorkflowEndpoints.cs (depends on T047, T012, T013)
- [ ] T049 [US5] Implement POST /workflow/sessions/{id}/steps/draft/preview endpoint (depends on T048, reuses DTO)
- [ ] T050 [US5] Ensure preview responses exclude prompt content from logs in WorkflowOrchestrator (depends on T012)

### UI実装

- [ ] T051 [US5] Add "プレビュー" checkbox toggle in WorkflowOutline.razor (depends on T028)
- [ ] T052 [US5] Display prompt full text when preview mode enabled in WorkflowOutline.razor (depends on T051, T048)
- [ ] T053 [US5] Display warning banner "⚠️ この内容は機微情報を含みます..." in WorkflowOutline.razor (depends on T052)
- [ ] T054 [US5] Add "プレビュー" checkbox toggle in WorkflowDraft.razor (depends on T043)
- [ ] T055 [US5] Display prompt full text when preview mode enabled in WorkflowDraft.razor (depends on T054, T049)

**Checkpoint**: User Story 5 独立動作 - プレビューモードが全ステップで機能

---

## Phase 7: User Story 3 - タイトル＆導入部生成 (Priority: P3)

**Goal**: 確定下書きから、タイトル案（3案）と導入部を生成し、選択・編集・確定できる

**Independent Test**: US2で下書き確定済み → タイトル生成 → 3案表示 → 選択/編集 → 確定が完了

### API実装

- [ ] T056 [US3] Implement POST /workflow/sessions/{id}/steps/titlehook/generate endpoint in src/BlogDraftWebApp.Api/Endpoints/WorkflowEndpoints.cs (depends on T025, T013, T012)
- [ ] T057 [US3] Implement POST /workflow/sessions/{id}/steps/titlehook/save endpoint (depends on T026, reuses DTOs)
- [ ] T058 [US3] Implement POST /workflow/sessions/{id}/steps/titlehook/confirm endpoint (depends on T027, reuses DTOs)
- [ ] T059 [US3] Add step order validation (reject titlehook generation if draft not confirmed) in WorkflowOrchestrator (depends on T042)

### UI実装

- [ ] T060 [P] [US3] Create WorkflowTitle.razor page in src/BlogDraftWebApp/Components/Pages/WorkflowTitle.razor
- [ ] T061 [US3] Implement title generation UI with 3 option cards in WorkflowTitle.razor (depends on T060, T056)
- [ ] T062 [US3] Implement title selection and edit mode in WorkflowTitle.razor (depends on T061)
- [ ] T063 [US3] Implement confirm and mark workflow as Completed in WorkflowTitle.razor (depends on T062, T058)
- [ ] T064 [US3] Add session resume logic in WorkflowTitle.razor OnInitializedAsync (depends on T063, T033)
- [ ] T065 [US3] Implement "再生成" button to regenerate 3 title options in WorkflowTitle.razor (depends on T061, T056)

**Checkpoint**: User Story 3 独立動作 - 全ステップ（アウトライン→下書き→タイトル）が完全機能

---

## Phase 8: User Story 6 - セッション管理（削除と保管期限） (Priority: P3)

**Goal**: ユーザーがセッションを手動削除でき、30日経過後に自動削除される

**Independent Test**: セッション削除API呼び出し → セッション削除確認、30日後の自動削除をシミュレート

### API実装

- [ ] T066 [US6] Implement DELETE /workflow/sessions/{id} endpoint in src/BlogDraftWebApp.Api/Endpoints/WorkflowEndpoints.cs (depends on T012)
- [ ] T067 [P] [US6] Create SessionListResponse DTO in src/BlogDraftWebApp.Api/Models/SessionListResponse.cs
- [ ] T068 [US6] Implement GET /workflow/sessions endpoint (list all sessions) in src/BlogDraftWebApp.Api/Endpoints/WorkflowEndpoints.cs (depends on T067, T012)

### 自動削除バッチ

- [ ] T069 [US6] Implement IHostedService for session cleanup batch in src/BlogDraftWebApp/Services/SessionCleanupService.cs (depends on T010, T014)
- [ ] T070 [US6] Add cleanup schedule configuration to appsettings.json (depends on T015)
- [ ] T071 [US6] Register SessionCleanupService in Program.cs (depends on T069)

### 管理者UI

- [ ] T072 [P] [US6] Create SessionList.razor page in src/BlogDraftWebApp/Components/Pages/SessionList.razor
- [ ] T073 [US6] Implement session list display (without sensitive data) in SessionList.razor (depends on T072, T068)
- [ ] T074 [US6] Implement delete button with confirmation dialog in SessionList.razor (depends on T073, T066)
- [ ] T075 [US6] Display warning banner for sessions expiring within 5 days in SessionList.razor (depends on T073)
- [ ] T076 [P] [US6] Add navigation link "管理" to main menu

**Checkpoint**: User Story 6 独立動作 - セッション管理機能が完全機能

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: テスト、ドキュメント、統合、デプロイ準備

### ユニットテスト

- [ ] T077 [P] Write unit tests for WorkflowSession state transitions in tests/BlogDraftWebApp.Core.UnitTests/Models/WorkflowSessionTests.cs
- [ ] T078 [P] Write unit tests for WorkflowOrchestrator in tests/BlogDraftWebApp.Core.UnitTests/Services/WorkflowOrchestratorTests.cs
- [ ] T079 [P] Write unit tests for LiteDbWorkflowRepository (with temp DB) in tests/BlogDraftWebApp.Core.UnitTests/Services/LiteDbWorkflowRepositoryTests.cs
- [ ] T080 [P] Write unit tests for PromptComposer step-specific logic in tests/BlogDraftWebApp.Core.UnitTests/Services/PromptComposerTests.cs

### 統合テスト

- [ ] T081 [P] Write integration test for POST /workflow/sessions in tests/BlogDraftWebApp.Api.IntegrationTests/WorkflowEndpointsTests.cs
- [ ] T082 [P] Write integration test for workflow step generation with step order validation in tests/BlogDraftWebApp.Api.IntegrationTests/WorkflowEndpointsTests.cs
- [ ] T083 [P] Write integration test for workflow preview endpoints (no LLM call) in tests/BlogDraftWebApp.Api.IntegrationTests/WorkflowEndpointsTests.cs
- [ ] T084 [P] Write integration test for session expiration (410 Gone) in tests/BlogDraftWebApp.Api.IntegrationTests/WorkflowEndpointsTests.cs

### 一致性・排他・ログの横断検証

- [ ] T103 [P] Add integration test to verify preview prompt equals executed prompt (capture request in stub ILlmClient) in tests/BlogDraftWebApp.Api.IntegrationTests/WorkflowEndpointsTests.cs
- [ ] T104 [P] Add integration test for SESSION_BUSY (409) when concurrent generate requests are sent for the same session in tests/BlogDraftWebApp.Api.IntegrationTests/WorkflowEndpointsTests.cs
- [ ] T105 [P] Add integration test to ensure API responses/logs do not contain request bodies or prompt content (smoke) in tests/BlogDraftWebApp.Api.IntegrationTests/WorkflowEndpointsTests.cs

### セッション/プロンプトのログ方針

- [ ] T106 Document allowed log fields (sessionId, step, errorCode, durationMs, success/failure) in specs/002-workflow-draft-generation/spec.md and ensure implementation follows it
- [ ] T107 Ensure GlobalExceptionHandler logs Exception.ToString() while avoiding sensitive context in messages in src/BlogDraftWebApp.Api/Middleware/GlobalExceptionHandler.cs

### LLM タイムアウト（60秒）

- [ ] T108 Ensure 60s timeout is enforced via CancellationToken/SDK options in src/BlogDraftWebApp.Core/Services/OpenAIClient.cs (or existing LLM client) and covered by unit test in tests/BlogDraftWebApp.Core.UnitTests/Services/OpenAIClientTests.cs

### 最終成果物のコピー（ダウンロード不要）

- [ ] T109 [P] Add copy-to-clipboard JS interop helper in src/BlogDraftWebApp/wwwroot/ and src/BlogDraftWebApp/Services/ClipboardService.cs
- [ ] T110 Add "コピー" button (one action) for the final confirmed result in src/BlogDraftWebApp/Components/Pages/WorkflowTitle.razor (depends on T109)
- [ ] T111 [P] Add E2E test for one-click copy availability on final step in tests/BlogDraftWebApp.E2E.Tests/Pages/WorkflowJourneyTests.cs

---

### E2Eテスト

- [ ] T085 [P] Write E2E test for complete workflow journey (outline→draft→title) in tests/BlogDraftWebApp.E2E.Tests/Pages/WorkflowJourneyTests.cs
- [ ] T086 [P] Write E2E test for session resume after page reload in tests/BlogDraftWebApp.E2E.Tests/Pages/WorkflowJourneyTests.cs
- [ ] T087 [P] Write E2E test for preview mode with warning banner in tests/BlogDraftWebApp.E2E.Tests/Pages/WorkflowJourneyTests.cs
- [ ] T088 [P] Write E2E test for session deletion in tests/BlogDraftWebApp.E2E.Tests/Pages/WorkflowJourneyTests.cs

### ドキュメント

- [ ] T089 [P] Create quickstart.md with workflow setup instructions in specs/002-workflow-draft-generation/quickstart.md
- [ ] T090 [P] Update main README.md with workflow feature description
- [ ] T091 [P] Create API documentation (OpenAPI export) in specs/002-workflow-draft-generation/contracts/openapi.yaml

### 既存機能との統合

- [ ] T092 Update GlobalExceptionHandler to catch new workflow exceptions in src/BlogDraftWebApp.Api/Middleware/GlobalExceptionHandler.cs
- [ ] T093 Register workflow services in Program.cs DI container
- [ ] T094 Verify existing single-shot generation (/draft) still works alongside workflow

### 本番準備

- [ ] T095 Add workflow database path to Azure App Service configuration
- [ ] T096 Test session cleanup batch in staging environment
- [ ] T097 Run all tests (unit, integration, E2E) in CI pipeline
- [ ] T098 Deploy to production and verify workflow functionality

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 依存なし - 即座に開始可能
- **Foundational (Phase 2)**: Setup完了後 - **全ユーザーストーリーをブロック**
- **User Stories (Phase 3-8)**: Foundational完了後
  - US1 (Phase 3): Foundational後に開始可能
  - US4 (Phase 4): US1完了後（US1と密接に統合）
  - US2 (Phase 5): Foundational後に開始可能（US1と並行可能だが、順序推奨）
  - US5 (Phase 6): US1/US2のAPI/UI完了後に追加可能
  - US3 (Phase 7): Foundational後に開始可能（US2完了推奨）
  - US6 (Phase 8): Foundational後に開始可能（独立）
- **Polish (Phase 9)**: 全ユーザーストーリー完了後

### User Story Dependencies

- **US1 (P1)**: Foundational後に開始 - 他ストーリーに依存なし
- **US4 (P1)**: US1完了後（US1の永続化を担保）
- **US2 (P2)**: Foundational後に開始可能 - US1確定後にテスト
- **US5 (P2)**: US1/US2のUI完成後に追加
- **US3 (P3)**: Foundational後に開始可能 - US2確定後にテスト
- **US6 (P3)**: Foundational後に開始可能 - 完全独立

### Within Each Phase

- Foundational内: データモデル → 永続化 → オーケストレーション → プロンプト拡張
- 各ユーザーストーリー内: API実装 → UI実装 → 統合
- API内: DTO → エンドポイント
- UI内: ページ作成 → ロジック実装 → API統合

### Parallel Opportunities

- **Setup (Phase 1)**: T001-T003 すべて並行可能
- **Foundational (Phase 2)**:
  - T004-T007 (データモデル) 並行可能
  - T014-T017 (設定・例外) 並行可能
- **US1 API**: T018-T023 (DTO) すべて並行可能
- **各Phase内**: [P] マークのタスクは並行実行可能
- **ユーザーストーリー間**: Foundational完了後、US1/US2/US3/US6 は異なるチームメンバーが並行作業可能

---

## Parallel Example: Foundational Phase

```bash
# データモデルを並行生成:
Task T004: WorkflowStep.cs
Task T005: RagSnapshot.cs
Task T006: Outline.cs
Task T007: TitleHook.cs
# 完了後:
Task T008: WorkflowSession.cs (T004-T007に依存)

# 設定・例外を並行生成（データモデルと独立）:
Task T014: WorkflowOptions.cs
Task T015: appsettings.json更新
Task T016: InvalidStateTransitionException.cs
Task T017: SessionNotFoundException.cs
```

---

## Implementation Strategy

### MVP First (US1 + US4 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL** - すべてのブロッカー解除)
3. Complete Phase 3: US1 (アウトライン生成)
4. Complete Phase 4: US4 (状態保存・再開)
5. **STOP and VALIDATE**: US1+US4を独立テスト
6. デプロイ/デモ準備完了（MVP！）

### Incremental Delivery

1. Setup + Foundational → 基盤完成
2. Add US1 + US4 → 独立テスト → デプロイ/デモ (MVP!)
3. Add US2 → 独立テスト → デプロイ/デモ
4. Add US5 → 独立テスト → デプロイ/デモ
5. Add US3 → 独立テスト → デプロイ/デモ
6. Add US6 → 独立テスト → デプロイ/デモ
7. 各ストーリー追加ごとに価値提供、既存機能を破壊しない

### Parallel Team Strategy

複数開発者がいる場合:

1. チーム全体で Setup + Foundational を完了
2. Foundational完了後:
   - Developer A: US1 + US4（緊密統合のため同一担当推奨）
   - Developer B: US2
   - Developer C: US6（完全独立）
3. US1/US2完了後:
   - Developer A: US5（US1/US2に追加）
   - Developer B: US3
4. ストーリー完成後に統合、独立テスト

---

## Notes

- [P] = 異なるファイル、依存なし、並行実行可能
- [Story] = タスクが属するユーザーストーリー（トレーサビリティ）
- 各ユーザーストーリーは独立して完成・テスト可能
- テストは実装前に失敗することを確認
- 各タスクまたは論理グループごとにコミット
- Checkpointで停止してストーリーを独立検証
- 避けるべき: 曖昧なタスク、同一ファイル競合、ストーリー独立性を破壊する横断依存

---

**Task Generation Complete**

- **Total Tasks**: T001-T119
- **User Stories Covered**: 6 (US1, US4, US2, US5, US3, US6)
- **Parallel Opportunities**: tasks marked [P]
- **MVP Scope**: Phase 1-4 (T001-T038) = US1 + US4 = アウトライン生成と状態再開
- **Independent Testing**: 各ユーザーストーリー完了時点でCheckpoint設定

**Suggested First Sprint**: Phase 1-4 (Setup → Foundational → US1 → US4) = MVP delivery
