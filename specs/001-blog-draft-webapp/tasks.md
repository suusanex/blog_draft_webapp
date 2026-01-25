# Implementation Tasks: ブログ下書き生成 Web アプリケーション（テスト完全実装版）

**Branch**: `001-blog-draft-webapp` | **Date**: 2026-01-24  
**Feature**: ブログ下書き生成ツール（Web アプリ移植 + 完全テストカバレッジ）  
**Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

---

## 📋 Implementation Strategy

### MVP Scope (User Story 1)
最小限の価値提供として、User Story 1（ブログ下書き生成）の実装と完全なテストカバレッジを最優先とする。

### Test-First Approach
- 各機能実装時に対応するテスト（Unit/Integration/E2E）を同時実装
- テストカバレッジ目標: 行 80%、分岐 70%
- CI パイプラインでの自動実行を前提

### Parallel Execution
- [P] マーカー付きタスクは、異なるファイルを操作するため並列実行可能
- 同一ファイルへの変更や依存関係のあるタスクは順次実行

---

## Phase 1: Setup & Infrastructure

**Goal**: プロジェクト基盤とテスト環境の構築

### Tasks

- [ ] T001 Create solution structure per implementation plan in src/ and tests/
- [ ] T002 [P] Add NuGet packages to BlogDraftWebApp.Core.csproj (Azure.AI.OpenAI, Azure.Search.Documents, Microsoft.Extensions.*)
- [ ] T003 [P] Add NuGet packages to BlogDraftWebApp.Api.csproj (ASP.NET Core Minimal API dependencies)
- [ ] T004 [P] Add NuGet packages to BlogDraftWebApp.csproj (Blazor Server, Markdig, Azure.Identity)
- [ ] T005 [P] Add NuGet packages to BlogDraftWebApp.Core.UnitTests.csproj (NUnit, Moq, coverlet.collector)
- [ ] T006 [P] Add NuGet packages to BlogDraftWebApp.Api.IntegrationTests.csproj (Microsoft.AspNetCore.Mvc.Testing, NUnit, Moq)
- [x] T007 Create new test project BlogDraftWebApp.Components.Tests.csproj with bunit, bunit.web, NUnit, Moq
- [x] T008 Create new test project BlogDraftWebApp.E2E.Tests.csproj with Microsoft.Playwright, Microsoft.Playwright.NUnit
- [ ] T009 [P] Install Playwright browsers via `pwsh bin/Debug/net10.0/playwright.ps1 install`
- [ ] T010 Configure appsettings.json and appsettings.Development.json (without secrets, with placeholders)
- [ ] T011 Setup User Secrets for local development (dotnet user-secrets init)
- [ ] T012 Create .gitignore entries for secrets and test output files

---

## Phase 2: Foundational Components

**Goal**: すべてのユーザーストーリーで使用される基盤コンポーネント

### Core Models

- [ ] T013 [P] Create BlogOverview.cs in src/BlogDraftWebApp.Core/Models/ with Content property and validation attributes
- [ ] T014 [P] Create StyleCard.cs in src/BlogDraftWebApp.Core/Models/ with Title, SystemPrompt, Content properties
- [ ] T015 [P] Create RAGChunk.cs in src/BlogDraftWebApp.Core/Models/ with Text, Score, SourceTitle, SourceUrl properties
- [ ] T016 [P] Create Prompt.cs in src/BlogDraftWebApp.Core/Models/ with SystemMessage, RagContext, UserOverview, FullPrompt properties
- [ ] T017 [P] Create Draft.cs in src/BlogDraftWebApp.Core/Models/ with Content, Model, GeneratedAt, TokensUsed properties

### Core Models - Unit Tests

- [ ] T018 [P] Create BlogOverviewTests.cs in tests/BlogDraftWebApp.Core.UnitTests/Models/ testing validation rules (min/max length)
- [ ] T019 [P] Create StyleCardTests.cs testing required properties and initialization
- [ ] T020 [P] Create RAGChunkTests.cs testing property initialization and edge cases (null SourceUrl)
- [ ] T021 [P] Create PromptTests.cs testing FullPrompt composition logic
- [ ] T022 [P] Create DraftTests.cs testing property assignments and defaults

### Configuration Classes

- [ ] T023 [P] Create LlmOptions.cs in src/BlogDraftWebApp.Core/Configuration/ with ApiKey, Model, MaxTokens, Temperature, Timeout properties
- [ ] T024 [P] Create RagOptions.cs with Enabled, Endpoint, IndexName, TopK, MinimumScore properties
- [ ] T025 [P] Create StyleCardOptions.cs with Title, SystemPrompt, Content properties
- [ ] T026 [P] Create LlmOptionsValidator.cs implementing IValidateOptions<LlmOptions> (ApiKey required, Timeout > 0)
- [ ] T027 [P] Create RagOptionsValidator.cs implementing IValidateOptions<RagOptions> (Endpoint required if Enabled)
- [ ] T028 [P] Create StyleCardOptionsValidator.cs implementing IValidateOptions<StyleCardOptions> (SystemPrompt and Content required)
- [ ] T029 [P] Create StyleCardPostConfigure.cs implementing IPostConfigureOptions<StyleCardOptions> to load from file if path provided

### Configuration - Unit Tests

- [ ] T030 [P] Create LlmOptionsValidatorTests.cs testing all validation rules (missing ApiKey, invalid Timeout, etc.)
- [ ] T031 [P] Create RagOptionsValidatorTests.cs testing Enabled=true without Endpoint fails
- [ ] T032 [P] Create StyleCardOptionsValidatorTests.cs testing missing SystemPrompt/Content fails
- [ ] T033 [P] Create StyleCardPostConfigureTests.cs testing file load success and failure scenarios

### Exception Classes

- [ ] T034 [P] Create ConfigurationException.cs in src/BlogDraftWebApp.Core/Exceptions/ with ErrorCode, Details properties
- [ ] T035 [P] Create LlmException.cs with IsRetryable, ErrorCode, OriginalException properties
- [ ] T036 [P] Create RagException.cs with IsRetryable, ErrorCode, OriginalException properties

---

## Phase 3: User Story 1 - ブログ下書き生成 [US1]

**Goal**: コア機能の実装と完全テストカバレッジ

**Independent Test**: ユーザーが概要を入力して「生成」ボタンを押すと、Markdown 形式の下書きが表示され、コピー可能。

### Core Services - Interfaces

- [ ] T037 [P] [US1] Create IPromptComposer.cs in src/BlogDraftWebApp.Core/Services/ with ComposeAsync(BlogOverview, RAGChunk[], StyleCard, CancellationToken) method
- [ ] T038 [P] [US1] Create IRetrievalService.cs with RetrieveAsync(string query, CancellationToken) returning (RAGChunk[], warning?) method
- [ ] T039 [P] [US1] Create ILlmClient.cs with GenerateAsync(Prompt, CancellationToken) returning Draft method
- [ ] T040 [P] [US1] Create IConfigurationValidator.cs with ValidateOnStartup() method

### Core Services - Implementations

- [ ] T041 [US1] Implement PromptComposer.cs combining SystemPrompt, RAG context, user overview into Prompt.FullPrompt
- [ ] T042 [US1] Implement AzureAISearchService.cs calling Azure.Search.Documents SDK with vector search, top-K filtering, score threshold
- [ ] T043 [US1] Implement OpenAIClient.cs calling Azure.AI.OpenAI SDK with timeout handling, error classification (retryable/non-retryable)
- [ ] T044 [US1] Implement ConfigurationValidator.cs checking all required settings on startup, logging errors
- [ ] T045 [US1] Implement LlmErrorClassifier.cs to classify LLM API errors (401 → non-retryable, 429 → retryable, timeout → retryable)

### Core Services - Unit Tests

- [ ] T046 [P] [US1] Create PromptComposerTests.cs testing RAG chunk formatting, empty RAG handling, system prompt inclusion
- [ ] T047 [P] [US1] Create AzureAISearchServiceTests.cs with mocked SearchClient testing top-K, score filtering, exception handling
- [ ] T048 [P] [US1] Create OpenAIClientTests.cs with mocked Azure.AI.OpenAI client testing timeout, retryable errors, success cases
- [ ] T049 [P] [US1] Create ConfigurationValidatorTests.cs testing startup validation for missing/invalid config
- [ ] T050 [P] [US1] Create LlmErrorClassifierTests.cs testing error classification logic (401, 429, timeout, unknown)

### API Models

- [ ] T051 [P] [US1] Create GenerateDraftRequest.cs in src/BlogDraftWebApp.Api/Models/ with Overview property [Required, MinLength(10), MaxLength(5000)]
- [ ] T052 [P] [US1] Create GenerateDraftResponse.cs with Draft, Model, GeneratedAt, RagHitCount, Warning? properties
- [ ] T053 [P] [US1] Create ErrorResponse.cs with ErrorCode, Message, Details?, RequestId, IsRetryable properties

### API Endpoints

- [ ] T054 [US1] Implement /draft POST endpoint in src/BlogDraftWebApp.Api/Endpoints/DraftEndpoints.cs calling RetrievalService → PromptComposer → LlmClient
- [ ] T055 [US1] Add input validation to /draft endpoint (Overview null/empty/too short → 400 BadRequest with ErrorResponse)
- [ ] T056 [US1] Add error handling to /draft endpoint (LlmException → 500/503 with IsRetryable, RagException → 500/RAG_ERROR and stop)
- [ ] T057 [US1] Add short output warning to /draft endpoint (Draft.Content.Length < 100 → add warning message)
- [ ] T058 [P] [US1] Implement /health GET endpoint in src/BlogDraftWebApp.Api/Endpoints/HealthEndpoints.cs returning 200 OK

### API Endpoints - Integration Tests

- [ ] T059 [US1] Create DraftEndpointsTests.cs in tests/BlogDraftWebApp.Api.IntegrationTests/ with TestWebApplicationFactory
- [ ] T060 [P] [US1] Add test: POST /draft with valid input returns 200 with Draft content
- [ ] T061 [P] [US1] Add test: POST /draft with empty Overview returns 400 BadRequest with ErrorResponse
- [ ] T062 [P] [US1] Add test: POST /draft with 9-char Overview returns 400 BadRequest
- [ ] T063 [P] [US1] Add test: POST /draft with 5001-char Overview returns 400 BadRequest
- [ ] T064 [P] [US1] Add test: POST /draft with mocked LLM timeout returns 500 with IsRetryable=true
- [ ] T065 [P] [US1] Add test: POST /draft with mocked RAG failure returns RAG_ERROR
- [ ] T066 [P] [US1] Add test: POST /draft with short output (<100 chars) adds warning
- [ ] T067 [P] [US1] Add test: GET /health returns 200 OK even with invalid config

### Blazor UI Components

- [ ] T068 [US1] Create GenerateDraft.razor in src/BlogDraftWebApp/Components/Pages/ with @page "/generate"
- [ ] T069 [US1] Add EditForm with InputTextArea for Overview input (10-5000 char validation)
- [ ] T070 [US1] Add DataAnnotationsValidator and ValidationMessage for Overview field
- [ ] T071 [US1] Add generate button with disabled state when Overview is empty or _isBusy is true
- [ ] T072 [US1] Implement GenerateAsync() method calling POST /draft via HttpClient, handling success/error responses
- [ ] T073 [US1] Add Markdown rendering using Markdig.ToHtml() with DisableHtml() for security
- [ ] T074 [US1] Add copy button calling JS interop `navigator.clipboard.writeText()`
- [ ] T075 [US1] Add error display with retry button (shown only if ErrorResponse.IsRetryable is true)
- [ ] T076 [US1] Add warning display for short output and RAG warnings
- [ ] T077 [US1] Add loading spinner during _isBusy state

### Blazor UI - bUnit Tests

- [x] T078 [US1] Create GenerateDraftTests.cs in tests/BlogDraftWebApp.Components.Tests/Pages/
- [x] T079 [P] [US1] Setup test: Mock IHttpClientFactory, IJSRuntime, NavigationManager in test context
- [ ] T080 [P] [US1] Test: OnOverviewChanged updates _model.Overview and triggers StateHasChanged
- [x] T081 [P] [US1] Test: IsGenerateDisabled returns true when Overview is empty
- [ ] T082 [P] [US1] Test: IsGenerateDisabled returns true when _isBusy is true
- [x] T083 [P] [US1] Test: IsGenerateDisabled returns false when Overview is valid and not busy
- [ ] T084 [P] [US1] Test: GenerateAsync sets _isBusy=true, calls HttpClient POST /draft, sets _isBusy=false
- [x] T085 [P] [US1] Test: GenerateAsync with 200 response updates _draftMarkdown and _draftHtml
- [x] T086 [P] [US1] Test: GenerateAsync with 400 response sets _error with appropriate message
- [x] T087 [P] [US1] Test: GenerateAsync with 500 retryable error sets _error with IsRetryable=true
- [ ] T088 [P] [US1] Test: Markdown rendering converts markdown to HTML correctly
- [x] T089 [P] [US1] Test: Markdown rendering disables raw HTML (XSS protection)
- [ ] T090 [P] [US1] Test: CopyDraftAsync calls JS interop with correct draft content
- [x] T091 [P] [US1] Test: CopyDraftAsync sets _copyMessage = "コピーしました"
- [ ] T092 [P] [US1] Test: RetryAsync calls GenerateAsync again
- [x] T093 [P] [US1] Test: Error display shows ErrorResponse.Message when _error is not null
- [x] T094 [P] [US1] Test: Retry button shows only when _error.IsRetryable is true
- [ ] T095 [P] [US1] Test: Warning display shows _warning message when not null
- [ ] T096 [P] [US1] Test: Loading state shows "生成中..." when _isBusy is true

### Dependency Injection Setup

- [ ] T097 [US1] Register services in src/BlogDraftWebApp/Program.cs (AddSingleton<StyleCard>, AddScoped<IPromptComposer>, etc.)
- [ ] T098 [US1] Register LlmOptions, RagOptions, StyleCardOptions with IOptions pattern
- [ ] T099 [US1] Register validators (AddSingleton<IValidateOptions<LlmOptions>, LlmOptionsValidator>)
- [ ] T100 [US1] Add startup validation calling IConfigurationValidator.ValidateOnStartup() in Program.cs
- [ ] T101 [US1] Configure HttpClient for Blazor components with AddHttpClient()
- [ ] T102 [US1] Map API endpoints with app.MapDraftEndpoints() and app.MapHealthEndpoints()

### E2E Tests - User Story 1

- [x] T103 [US1] Create GenerateDraftPageTests.cs in tests/BlogDraftWebApp.E2E.Tests/Pages/
- [ ] T104 [US1] Setup: Configure Playwright browser launch (headless mode for CI)
- [ ] T105 [US1] Setup: Create BrowserFixture.cs to manage browser lifecycle
- [x] T106 [P] [US1] E2E Test: Navigate to /generate, input valid overview, click generate button, verify draft displays
- [ ] T107 [P] [US1] E2E Test: Verify copy button copies draft to clipboard (mock clipboard API)
- [ ] T108 [P] [US1] E2E Test: Empty input disables generate button
- [ ] T109 [P] [US1] E2E Test: Input 9 characters, submit form, verify validation error displays
- [ ] T110 [P] [US1] E2E Test: Input 5001 characters, verify validation error displays
- [ ] T111 [P] [US1] E2E Test: Mock API error response, verify error message displays
- [ ] T112 [P] [US1] E2E Test: Mock retryable error, verify retry button appears and works
- [ ] T113 [P] [US1] E2E Test: Verify loading spinner displays during generation
- [ ] T114 [P] [US1] E2E Test: Verify warning message displays for short output

---

## Phase 4: User Story 2 - プレビューモード [US2]

**Goal**: デバッグ機能の実装とテスト

**Independent Test**: プレビューモードでプロンプト全文が表示され、LLM は呼び出されない。

### API Models

- [ ] T115 [P] [US2] Create PreviewPromptRequest.cs in src/BlogDraftWebApp.Api/Models/ with Overview property [Required, MinLength(10)]
- [ ] T116 [P] [US2] Create PreviewPromptResponse.cs with Prompt (string), RagHitCount, Warning? properties

### API Endpoints

- [ ] T117 [US2] Implement /draft/preview POST endpoint in DraftEndpoints.cs calling RetrievalService → PromptComposer (no LLM call)
- [ ] T118 [US2] Add input validation to /draft/preview (same as /draft endpoint)
- [ ] T119 [US2] Ensure /draft/preview does NOT call LlmClient.GenerateAsync() (test verification required)

### API Endpoints - Integration Tests

- [ ] T120 [P] [US2] Add test: POST /draft/preview with valid input returns 200 with Prompt text
- [ ] T121 [P] [US2] Add test: POST /draft/preview verifies LlmClient.GenerateAsync is never called (mock verification)
- [ ] T122 [P] [US2] Add test: POST /draft/preview with empty RAG returns prompt without RAG context
- [ ] T123 [P] [US2] Add test: POST /draft/preview prompt matches PromptComposer.ComposeAsync output exactly

### Blazor UI Components

- [ ] T124 [US2] Add preview mode checkbox to GenerateDraft.razor with @bind-Value="_isPreviewMode"
- [ ] T125 [US2] Implement PreviewAsync() method calling POST /draft/preview via HttpClient
- [ ] T126 [US2] Add preview result display with <pre> tag for prompt text
- [ ] T127 [US2] Add warning banner "この内容には機密情報が含まれる可能性があります" above preview result
- [ ] T128 [US2] Add copy button for preview prompt calling CopyPromptAsync() with JS interop
- [ ] T129 [US2] Modify SubmitAsync() to call PreviewAsync() if _isPreviewMode is true, else GenerateAsync()
- [ ] T130 [US2] Clear _promptPreview when switching from preview to generate mode

### Blazor UI - bUnit Tests

- [ ] T131 [P] [US2] Test: PreviewAsync sets _isBusy=true, calls HttpClient POST /draft/preview, sets _isBusy=false
- [ ] T132 [P] [US2] Test: PreviewAsync with 200 response updates _promptPreview
- [ ] T133 [P] [US2] Test: PreviewAsync does not update _draftMarkdown (draft should remain null)
- [ ] T134 [P] [US2] Test: SubmitAsync calls PreviewAsync when _isPreviewMode=true
- [ ] T135 [P] [US2] Test: SubmitAsync calls GenerateAsync when _isPreviewMode=false
- [ ] T136 [P] [US2] Test: CopyPromptAsync calls JS interop with _promptPreview content
- [ ] T137 [P] [US2] Test: CopyPromptAsync sets _copyPromptMessage = "コピーしました"
- [ ] T138 [P] [US2] Test: Preview mode checkbox toggles _isPreviewMode value
- [x] T139 [P] [US2] Test: Warning banner displays when _promptPreview is not null

### E2E Tests - User Story 2

- [x] T140 [P] [US2] E2E Test: Navigate to /generate, check preview mode, input overview, click preview button, verify prompt displays
- [ ] T141 [P] [US2] E2E Test: Verify preview mode does not show draft markdown section
- [ ] T142 [P] [US2] E2E Test: Verify warning banner "機密情報が含まれる可能性" displays in preview mode
- [ ] T143 [P] [US2] E2E Test: Verify copy button in preview mode copies prompt to clipboard
- [ ] T144 [P] [US2] E2E Test: Switch from preview to generate mode, verify UI updates correctly

---

## Phase 5: User Story 3 - 設定管理 [US3]

**Goal**: 管理者向け設定管理とバリデーション

**Independent Test**: 管理者が設定を変更して再起動すると、新しい設定が適用される。

### Configuration Loading

- [ ] T145 [US3] Configure appsettings.json with sections: OpenAI, AzureAISearch, StyleCard
- [ ] T146 [US3] Add Azure Key Vault integration in Program.cs with Azure.Extensions.AspNetCore.Configuration.Secrets
- [ ] T147 [US3] Test loading secrets from User Secrets in Development environment
- [ ] T148 [US3] Test loading secrets from Azure Key Vault in Production environment

### Startup Validation

- [ ] T149 [US3] Implement ConfigurationValidator.ValidateOnStartup() checking LlmOptions, RagOptions, StyleCardOptions
- [ ] T150 [US3] Log detailed error messages when validation fails (missing API key, invalid endpoint, etc.)
- [ ] T151 [US3] Throw exception on startup if critical config is missing (fail-fast behavior)

### Configuration - Integration Tests

- [ ] T152 [P] [US3] Create ConfigurationValidatorIntegrationTests.cs testing startup validation with invalid config
- [ ] T153 [P] [US3] Add test: App startup fails when LlmOptions.ApiKey is missing
- [ ] T154 [P] [US3] Add test: App startup fails when StyleCardOptions.Content is missing
- [ ] T155 [P] [US3] Add test: App startup succeeds when RAG is disabled (Enabled=false)
- [ ] T156 [P] [US3] Add test: App startup fails when RAG is enabled but Endpoint is missing
- [ ] T157 [P] [US3] Add test: StyleCard loads from file path if StyleCardOptions.FilePath is provided
- [ ] T158 [P] [US3] Add test: StyleCard loading fails gracefully with error log if file not found

### E2E Tests - User Story 3

- [ ] T159 [P] [US3] E2E Test: Start app with valid config, verify /health returns 200
- [ ] T160 [P] [US3] E2E Test: Start app with RAG disabled, verify draft generation works without RAG context

---

## Phase 6: Cross-Cutting Concerns & Polish

**Goal**: ロギング、エラーハンドリング、セキュリティ、パフォーマンス

### Logging & Error Handling

- [ ] T161 [P] Implement GlobalExceptionHandler.cs in src/BlogDraftWebApp.Api/Middleware/ implementing IExceptionHandler
- [ ] T162 [P] Add structured logging in all services with ILogger<T> (Info, Warning, Error levels)
- [ ] T163 [P] Add SensitiveDataFilter.cs to prevent logging of user input, RAG chunks, prompts, draft content (log only request IDs)
- [ ] T164 [P] Test GlobalExceptionHandler returns correct ErrorResponse for different exception types
- [ ] T165 [P] Test SensitiveDataFilter redacts sensitive fields from log output

### Security

- [ ] T166 [P] Verify API keys are never sent to browser (check network tab in E2E tests)
- [ ] T167 [P] Verify prompts and drafts are never logged (grep test output for sensitive strings)
- [ ] T168 [P] Add Content Security Policy headers in Program.cs
- [ ] T169 [P] Verify Markdig.DisableHtml() prevents XSS in draft rendering (bUnit test)

### Performance & Resilience

- [ ] T170 [P] Add timeout handling to HttpClient in AzureAISearchService and OpenAIClient (default 120s)
- [ ] T171 [P] Test timeout handling: mock slow HTTP response, verify timeout exception after 120s
- [ ] T172 [P] Add cancellation token propagation in all async methods
- [ ] T173 [P] Test cancellation: cancel request mid-flight, verify operation stops gracefully

### Documentation

- [ ] T174 [P] Create README.md in repository root with project overview, setup instructions, architecture diagram
- [ ] T175 [P] Create CONTRIBUTING.md with development workflow, testing guidelines, PR checklist
- [ ] T176 [P] Add XML documentation comments to all public APIs (interfaces, classes, methods)
- [ ] T177 [P] Generate API documentation with DocFX or similar tool (optional)

### CI/CD Pipeline

- [ ] T178 Create .github/workflows/ci.yml for GitHub Actions
- [ ] T179 Add CI job: dotnet build (all projects)
- [ ] T180 Add CI job: dotnet test (all test projects) with code coverage report
- [ ] T181 Add CI job: Playwright E2E tests with browser installation
- [ ] T182 Add CI job: coverage report upload to Codecov or similar
- [ ] T183 Add CI check: fail build if coverage < 80% line, < 70% branch
- [ ] T184 Add CI check: fail build if any test fails

---

## Phase 7: Final Validation & Deployment Prep

**Goal**: 完全なテストカバレッジ検証とデプロイ準備

### Test Coverage Validation

- [ ] T185 Run `dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover` on all test projects
- [ ] T186 Generate coverage report with ReportGenerator or similar tool
- [ ] T187 Verify line coverage >= 80%, branch coverage >= 70% (fail if below threshold)
- [ ] T188 Verify all E2E scenarios (minimum 10) are implemented and passing

### Integration Testing

- [ ] T189 Run all integration tests against TestWebApplicationFactory
- [ ] T190 Verify all API endpoints return correct status codes and response models
- [ ] T191 Verify error handling: invalid input, timeout, RAG failure, LLM failure

### E2E Testing

- [ ] T192 Run all Playwright E2E tests in headless mode
- [ ] T193 Verify all user stories have corresponding E2E tests
- [ ] T194 Capture screenshots on test failure for debugging

### Deployment Configuration

- [ ] T195 Create Dockerfile for containerized deployment
- [ ] T196 Create docker-compose.yml for local testing with mocked dependencies
- [ ] T197 Create Azure App Service deployment configuration (if using Azure)
- [ ] T198 Document environment variables required for production deployment
- [ ] T199 Create deployment checklist with security review, configuration validation

### Final Checks

- [ ] T200 Run full CI pipeline locally to verify all checks pass
- [ ] T201 Verify no secrets or API keys in codebase (git grep for common patterns)
- [ ] T202 Verify all TODO/FIXME comments are resolved or documented
- [ ] T203 Perform manual smoke test: deploy to staging, test all user stories end-to-end
- [ ] T204 Update CHANGELOG.md with all implemented features and tests

---

## 🛠 直近の修正

- [x] OpenAiLlmClient のリクエストで `max_completion_tokens` を指定するようにして OpenAI API の 400 エラーを回避

## 📊 Progress Tracking

### Completion Metrics

| Phase | Tasks | Status | Coverage |
|-------|-------|--------|----------|
| Phase 1: Setup | T001-T012 | ⏳ Not Started | - |
| Phase 2: Foundational | T013-T036 | ⏳ Not Started | - |
| Phase 3: User Story 1 | T037-T114 | ⏳ Not Started | Target: 80%+ |
| Phase 4: User Story 2 | T115-T144 | ⏳ Not Started | Target: 80%+ |
| Phase 5: User Story 3 | T145-T160 | ⏳ Not Started | Target: 80%+ |
| Phase 6: Polish | T161-T184 | ⏳ Not Started | - |
| Phase 7: Validation | T185-T204 | ⏳ Not Started | **Verify: 80% line, 70% branch** |

### Test Coverage Summary (To Be Updated)

| Test Type | Count | Status |
|-----------|-------|--------|
| Core Unit Tests | ~30 | ⏳ |
| Configuration Unit Tests | ~10 | ⏳ |
| bUnit Component Tests | ~20 | ⏳ |
| API Integration Tests | ~15 | ⏳ |
| E2E Playwright Tests | ~15 | ⏳ |
| **Total Tests** | **~90** | **⏳** |

---

## 🎯 Success Criteria

### Functional

- ✅ User Story 1: Draft generation works end-to-end
- ✅ User Story 2: Preview mode displays prompt without calling LLM
- ✅ User Story 3: Configuration management via environment variables/Key Vault

### Quality

- ✅ Line coverage >= 80%
- ✅ Branch coverage >= 70%
- ✅ E2E tests >= 10 scenarios
- ✅ All tests pass in CI pipeline
- ✅ No secrets in codebase
- ✅ No sensitive data in logs

### Performance

- ✅ Draft generation completes within 120s timeout
- ✅ Concurrent requests (up to 10 users) handled without degradation

---

## 📝 Notes

### Parallel Execution Strategy

Tasks marked with [P] can be executed in parallel as they operate on different files. Recommended batches:

**Batch 1** (Setup): T002, T003, T004, T005, T006, T007, T008, T009  
**Batch 2** (Models): T013, T014, T015, T016, T017  
**Batch 3** (Model Tests): T018, T019, T020, T021, T022  
**Batch 4** (Configuration): T023, T024, T025, T026, T027, T028, T029  
**Batch 5** (Config Tests): T030, T031, T032, T033  

### Test-First Approach

For each feature implementation task (e.g., T041 PromptComposer), immediately follow with corresponding test tasks (e.g., T046 PromptComposerTests). This ensures:
- Tests are written while implementation details are fresh
- Coverage targets are met incrementally
- Refactoring is safer with immediate test feedback

### Dependencies

```
Phase 1 → Phase 2 → Phase 3 → Phase 4 → Phase 5 → Phase 6 → Phase 7
                        ↓         ↓         ↓
                     [US1]     [US2]     [US3]
                        ↓         ↓         ↓
                    Tests     Tests     Tests
```

---

**Tasks Status**: ⏳ Ready for Implementation  
**Branch**: `001-blog-draft-webapp`  
**Total Tasks**: 204  
**Estimated Test Count**: ~90 tests
