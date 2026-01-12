# Tasks: ブログ下書き生成 Web アプリケーション

**Branch**: `001-blog-draft-webapp` | **Date**: 2026-01-11  
**Input**: Design documents from `/specs/001-blog-draft-webapp/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/openapi.yaml

**Feature Name**: ブログ下書き生成 Web アプリケーション

**Tests**: Constitution の「可能な限りUnitTestでのテストを行う」原則に従い、各実装タスク内にUnitTestを同梱するか、明示的なテストタスクで対応します。タスクの注記を参照してください。

**Organization**: タスクは User Story 単位でグループ化され、各ストーリーを独立して実装・テスト可能にしています。

---

## Format: `- [ ] [ID] [P?] [Story?] Description with file path`

- **[P]**: 並列実行可能（異なるファイル、依存関係なし）
- **[Story]**: このタスクが属する User Story（例: US1, US2, US3）
- 各タスクに正確なファイルパスを含む

---

## Phase 1: Setup（プロジェクト初期化）

**Purpose**: .NET ソリューション構造、プロジェクト作成、基本パッケージのインストール

- [ ] T001 リポジトリルートに .NET ソリューション作成: `BlogDraftWebApp.sln`
- [ ] T002 [P] Blazor Web App プロジェクト作成: `src/BlogDraftWebApp/BlogDraftWebApp.csproj`（Interactive Server モード）
- [ ] T003 [P] Core プロジェクト作成: `src/BlogDraftWebApp.Core/BlogDraftWebApp.Core.csproj`（.NET 9 クラスライブラリ）
- [ ] T004 [P] API プロジェクト作成: `src/BlogDraftWebApp.Api/BlogDraftWebApp.Api.csproj`（.NET 9 クラスライブラリ）
- [ ] T005 [P] UnitTests プロジェクト作成: `tests/BlogDraftWebApp.Core.UnitTests/BlogDraftWebApp.Core.UnitTests.csproj`（NUnit + Moq）
- [ ] T006 [P] IntegrationTests プロジェクト作成: `tests/BlogDraftWebApp.Api.IntegrationTests/BlogDraftWebApp.Api.IntegrationTests.csproj`（NUnit + WebApplicationFactory）
- [ ] T007 [P] Tests.Common プロジェクト作成: `tests/BlogDraftWebApp.Tests.Common/BlogDraftWebApp.Tests.Common.csproj`（共通テストヘルパー）
- [ ] T008 各プロジェクトの依存関係を設定（Core ← Api ← WebApp、Tests → Core/Api）
- [ ] T009 [P] 必須 NuGet パッケージをインストール: Azure.AI.OpenAI, Azure.Search.Documents
- [ ] T010 [P] User Secrets の初期化: `src/BlogDraftWebApp/` で `dotnet user-secrets init`
- [ ] T011 .gitignore に User Secrets、bin/、obj/、logs/ を追加
- [ ] T012 EditorConfig 設定: `/.editorconfig`（C# コーディング規約）

**Checkpoint**: ソリューション構造が完成し、すべてのプロジェクトがビルド可能

---

## Phase 2: Foundational（ブロッキング前提条件）

**Purpose**: 全 User Story の前提となるコア基盤。このフェーズが完了するまで User Story の作業は開始できない。

**⚠️ CRITICAL**: このフェーズ完了後に User Story 実装を開始

### 設定管理基盤

- [ ] T013 設定モデル作成: `src/BlogDraftWebApp.Core/Configuration/LlmOptions.cs`（Provider, ApiKey, BaseUrl, Model, MaxTokens, RequestTimeoutSeconds, Parameters）
- [ ] T014 [P] 設定モデル作成: `src/BlogDraftWebApp.Core/Configuration/RagOptions.cs`（Enabled, Endpoint, ApiKey, IndexName, TextFieldName, VectorFieldName, TopK, MinimumScore）
- [ ] T015 [P] 設定モデル作成: `src/BlogDraftWebApp.Core/Configuration/StyleCardOptions.cs`（Title, Content, SystemPrompt）
- [ ] T016 appsettings.json 作成: `src/BlogDraftWebApp/appsettings.json`（秘匿情報を含まないデフォルト値）
- [ ] T017 [P] appsettings.Development.json 作成: `src/BlogDraftWebApp/appsettings.Development.json`（開発用ログレベル）
- [ ] T018 Program.cs で設定読み込み設定: User Secrets → 環境変数 → Key Vault の優先順位を実装

### ロギング基盤

- [ ] T019 構造化ログ設定: `src/BlogDraftWebApp/Program.cs` で Serilog または Microsoft.Extensions.Logging 設定
- [ ] T020 ログフィルタ実装: `src/BlogDraftWebApp.Core/Logging/SensitiveDataFilter.cs`（API キー、プロンプト全文を自動除外）

### エラーハンドリング基盤

- [ ] T021 ErrorResponse モデル作成: `src/BlogDraftWebApp.Api/Models/ErrorResponse.cs`（ErrorCode, Message, Details, RequestId）
- [ ] T022 グローバル例外ハンドラミドルウェア作成: `src/BlogDraftWebApp.Api/Middleware/GlobalExceptionHandler.cs`（例外を ErrorResponse に変換）
- [ ] T022b [P] LLM 例外分類実装: `src/BlogDraftWebApp.Core/Services/LlmErrorClassifier.cs`（HTTP/SDK 例外から ErrorCode・メッセージを判定：認証エラー、レート制限、タイムアウト、サービスエラー等）
- [ ] T023 カスタム例外クラス作成: `src/BlogDraftWebApp.Core/Exceptions/LlmException.cs`、`RagException.cs`、`ConfigurationException.cs`

### DI 設定

- [ ] T024 DI コンテナ設定: `src/BlogDraftWebApp/Program.cs` で Options パターン、サービス登録、ライフタイム管理を実装
- [ ] T025 起動時設定バリデーション: `src/BlogDraftWebApp/Program.cs` で必須設定（API キー、文体カード）の存在確認、読み込み失敗時に起動失敗させる

**Checkpoint**: 基盤完成。設定・ログ・エラーハンドリングが動作し、User Story 実装を開始可能。

---

## Phase 3: User Story 1 - ブログ下書きの生成（Priority: P1）🎯 MVP

**Goal**: ユーザーが記事の概要を入力すると、RAG で過去記事を検索し、文体カードと合わせて LLM に送信し、Markdown 形式の下書きを生成して表示する。

**Independent Test**: ユーザーが概要を入力して「生成」ボタンを押すと、Markdown 下書きが画面に表示され、コピーボタンでクリップボードにコピーできる。

### コアモデル（US1）

- [ ] T026 [P] [US1] BlogOverview モデル作成: `src/BlogDraftWebApp.Core/Models/BlogOverview.cs`（Content プロパティ + バリデーション）
- [ ] T027 [P] [US1] StyleCard モデル作成: `src/BlogDraftWebApp.Core/Models/StyleCard.cs`（Title, Content, SystemPrompt）
- [ ] T028 [P] [US1] RAGChunk モデル作成: `src/BlogDraftWebApp.Core/Models/RAGChunk.cs`（Text, Score, SourceTitle, SourceUrl）
- [ ] T029 [P] [US1] Prompt モデル作成: `src/BlogDraftWebApp.Core/Models/Prompt.cs`（SystemMessage, RagContext, UserOverview, FullPrompt）
- [ ] T030 [P] [US1] Draft モデル作成: `src/BlogDraftWebApp.Core/Models/Draft.cs`（Content, Model, GeneratedAt, TokensUsed）

### RAG サービス（US1）

- [ ] T031 [US1] RAG 検索インターフェース作成: `src/BlogDraftWebApp.Core/Services/IRetrievalService.cs`（RetrieveAsync(query) → RAGChunk[]）
- [ ] T032 [US1] Azure AI Search 実装: `src/BlogDraftWebApp.Core/Services/AzureAISearchService.cs`（ベクトル検索、Top-K、スコア閾値、エラーハンドリング）
- [ ] T033 [US1] RAG 整形ロジック実装: `AzureAISearchService.cs` 内で重複除外、長さ制限（500文字）、出典情報付与
- [ ] T034 [US1] RAG フォールバック実装: 検索失敗時に空配列を返し、警告ログを出力
- [ ] T034b [US1] 生成結果の長さチェック実装: LLM 生成後、結果が 100 文字未満の場合は警告メッセージ「生成結果が短すぎます。入力内容を詳しくするか、設定を確認してください」を返却

### LLM クライアント（US1）

- [ ] T035 [US1] LLM クライアントインターフェース作成: `src/BlogDraftWebApp.Core/Services/ILlmClient.cs`（GenerateAsync(prompt) → Draft）
- [ ] T036 [US1] OpenAI 互換クライアント実装: `src/BlogDraftWebApp.Core/Services/OpenAIClient.cs`（Azure.AI.OpenAI SDK 使用、タイムアウト 120 秒、追加パラメータ対応）
- [ ] T037 [US1] タイムアウトハンドリング実装: `OpenAIClient.cs` で RequestTimeoutSeconds を適用、タイムアウト時に LlmException をスロー
- [ ] T037b [US1] 再試行可能なエラー判定: `OpenAIClient.cs` または `DraftEndpoints.cs` で再試行可能なエラー（タイムアウト、HTTP 503/429 等）を判定、レスポンスに "IsRetryable" フラグを含める

### プロンプト合成（US1）

- [ ] T038 [US1] プロンプト合成インターフェース作成: `src/BlogDraftWebApp.Core/Services/IPromptComposer.cs`（ComposeAsync(overview, ragChunks, styleCard) → Prompt）
- [ ] T039 [US1] プロンプト合成実装: `src/BlogDraftWebApp.Core/Services/PromptComposer.cs`（文体カード + RAG整形 + 概要を構造化して結合）
- [ ] T040 [US1] システムプロンプト構築: `PromptComposer.cs` で StyleCard.SystemPrompt + StyleCard.Content を system メッセージに設定
- [ ] T041 [US1] RAG セクション整形: `PromptComposer.cs` で RAGChunk 配列を "## 過去記事からの関連情報" セクションに整形（出典リンク含む）

### API エンドポイント（US1）

- [ ] T042 [US1] GenerateDraftRequest DTO 作成: `src/BlogDraftWebApp.Api/Models/GenerateDraftRequest.cs`（Overview + バリデーション属性）
- [ ] T043 [US1] GenerateDraftResponse DTO 作成: `src/BlogDraftWebApp.Api/Models/GenerateDraftResponse.cs`（Draft, Model, GeneratedAt, RagHitCount, Warning）
- [ ] T044 [US1] /draft エンドポイント実装: `src/BlogDraftWebApp.Api/Endpoints/DraftEndpoints.cs`（Minimal API）
  - バリデーション（空入力は 400 で拒否） → RAG 検索 → プロンプト合成 → LLM 生成 → レスポンス返却
  - RAG 失敗時は警告を追加して生成継続
  - エラー時は適切な HTTP ステータスと ErrorResponse を返却（IsRetryable フラグ含む）

### Blazor UI（US1）

- [ ] T045 [US1] 生成ページ Blazor コンポーネント作成: `src/BlogDraftWebApp/Components/Pages/GenerateDraft.razor`
  - 概要入力フォーム（Textarea、10〜5000 文字バリデーション）
  - 「下書きを生成」ボタン（**入力が空の場合は無効化**）
  - 生成結果表示エリア（Markdown レンダリング）
  - 「コピー」ボタン（Clipboard API でコピー、確認メッセージ表示）
  - エラー時に「再試行」ボタンを表示（IsRetryable フラグ判定）
- [ ] T046 [US1] 生成中ローディング表示: `GenerateDraft.razor` でスピナー + "生成中..." メッセージ
- [ ] T047 [US1] エラーメッセージ表示: `GenerateDraft.razor` で ErrorResponse を解析し、ユーザーフレンドリーなメッセージを表示
- [ ] T047b [US1] 再試行ボタン表示: `GenerateDraft.razor` で IsRetryable フラグが true の場合、「再試行」ボタンを表示し、同じリクエストを再度送信可能にする
- [ ] T048 [US1] 警告メッセージ表示: RAG 失敗時の警告（"関連記事の検索に失敗しました"）をアラートで表示

### DI 登録（US1）

- [ ] T049 [US1] サービス登録: `src/BlogDraftWebApp/Program.cs` で IRetrievalService、ILlmClient、IPromptComposer を DI コンテナに登録
- [ ] T050 [US1] 設定バインディング: `Program.cs` で LlmOptions、RagOptions、StyleCardOptions を Options パターンでバインド

### UnitTests（US1）

- [ ] T051 [P] [US1] PromptComposer テスト作成: `tests/BlogDraftWebApp.Core.UnitTests/Services/PromptComposerTests.cs`（RAGあり/なし、文体カード結合を検証、Assert.That 形式）
- [ ] T052 [P] [US1] AzureAISearchService テスト作成: `tests/BlogDraftWebApp.Core.UnitTests/Services/AzureAISearchServiceTests.cs`（モックで検索成功/失敗、重複除外、スコア閾値を検証、Assert.That 形式）
- [ ] T053 [P] [US1] OpenAIClient テスト作成: `tests/BlogDraftWebApp.Core.UnitTests/Services/OpenAIClientTests.cs`（モックでタイムアウト、エラーハンドリングを検証、Assert.That 形式）

**Checkpoint**: User Story 1 が完全に動作。ユーザーが概要を入力すると、RAG + 文体カード + LLM で下書きが生成される。

---

## Phase 4: User Story 2 - LLM 入力内容のプレビュー（Priority: P2）

**Goal**: 「プレビューモード」を選択すると、LLM に送信される予定のプロンプト全文（文体カード + RAG + 概要）を LLM API 呼び出しなしで画面に表示する。

**Independent Test**: ユーザーが「プレビューモード」を有効にして概要を入力し、「プレビュー」ボタンを押すと、プロンプト全文が画面に表示される（LLM API は呼び出されない）。

### API エンドポイント（US2）

- [ ] T054 [US2] PreviewPromptRequest DTO 作成: `src/BlogDraftWebApp.Api/Models/PreviewPromptRequest.cs`（Overview + バリデーション、GenerateDraftRequest と同一ルール）
- [ ] T055 [US2] PreviewPromptResponse DTO 作成: `src/BlogDraftWebApp.Api/Models/PreviewPromptResponse.cs`（Prompt, RagHitCount, Warning）
- [ ] T056 [US2] /draft/preview エンドポイント実装: `src/BlogDraftWebApp.Api/Endpoints/DraftEndpoints.cs`
  - バリデーション → RAG 検索 → プロンプト合成 → プロンプト全文を返却（LLM API は呼び出さない）
  - ログに Prompt.FullPrompt を記録しない（仕様で明示）

### Blazor UI（US2）

- [ ] T057 [US2] プレビューモード切替 UI 追加: `src/BlogDraftWebApp/Components/Pages/GenerateDraft.razor`
  - チェックボックス: "プレビューモード（LLM 入力内容を表示）"
  - プレビューモード有効時は "プレビュー" ボタンに切り替え
- [ ] T058 [US2] プロンプトプレビュー表示エリア: `GenerateDraft.razor`
  - プロンプト全文を整形して表示（コードブロックまたは Textarea）
  - 「コピー」ボタン（プロンプト全文をクリップボードにコピー）
- [ ] T059 [US2] 機密情報警告メッセージ表示: プレビュー結果表示前に警告アラート: "この内容には機密情報が含まれる可能性があります。画面のスクリーンショットや共有にご注意ください"

### ログ除外（US2）

- [ ] T060 [US2] プレビューモードのログ記録禁止: `src/BlogDraftWebApp.Api/Endpoints/DraftEndpoints.cs` で /draft/preview のレスポンスをログに記録しない設定を追加
- [ ] T061 [US2] SensitiveDataFilter 更新: `src/BlogDraftWebApp.Core/Logging/SensitiveDataFilter.cs` で "Prompt" フィールドを自動除外

### UnitTests（US2）

- [ ] T062 [P] [US2] /draft/preview エンドポイントテスト作成: `tests/BlogDraftWebApp.Api.IntegrationTests/DraftEndpointsTests.cs`
  - プレビューモードで LLM API が呼び出されないことを検証（モックの呼び出し回数 = 0）
  - プロンプト全文が実際の生成時と 100% 一致することを検証
  - 再試行可能エラー判定のテスト（タイムアウト/503/429）を含む

**Checkpoint**: User Story 2 が独立して動作。プレビューモードで LLM 入力内容を確認でき、LLM API コストが発生しない。

---

## Phase 5: User Story 3 - 設定の管理（Priority: P3 - 管理者向け）

**Goal**: Web アプリ管理者が、環境変数または Secret Store を通じて LLM・RAG・文体カードの設定を管理できる。起動時に設定不備を検出し、エラーログを出力する。

**Independent Test**: 管理者が Azure ポータルから設定を変更し、Web アプリを再起動すると、新しい設定が適用される。

### 設定バリデーション（US3）

- [ ] T063 [US3] LlmOptions バリデーション実装: `src/BlogDraftWebApp.Core/Configuration/LlmOptionsValidator.cs`（ApiKey, Model が必須、RequestTimeoutSeconds が 0 なら 120 をデフォルト）
- [ ] T064 [P] [US3] RagOptions バリデーション実装: `src/BlogDraftWebApp.Core/Configuration/RagOptionsValidator.cs`（Enabled=true の場合、Endpoint, ApiKey, IndexName が必須）
- [ ] T065 [P] [US3] StyleCardOptions バリデーション実装: `src/BlogDraftWebApp.Core/Configuration/StyleCardOptionsValidator.cs`（Content, SystemPrompt が必須）

### 起動時設定チェック（US3）

- [ ] T066 [US3] 設定検証サービス作成: `src/BlogDraftWebApp.Core/Services/IConfigurationValidator.cs`（ValidateAsync() → bool）
- [ ] T067 [US3] 設定検証実装: `src/BlogDraftWebApp.Core/Services/ConfigurationValidator.cs`（全 Options のバリデータを呼び出し、エラーをログに記録）
- [ ] T068 [US3] Program.cs で起動時検証: `src/BlogDraftWebApp/Program.cs`
  - アプリ起動時に ConfigurationValidator.ValidateAsync() を実行
  - 必須設定が不正な場合、エラーログを出力し、管理者向けメッセージ "システムが正しく構成されていません" を表示

### エラー表示（US3）

- [ ] T069 [US3] 設定エラーページ作成: `src/BlogDraftWebApp/Components/Pages/ConfigurationError.razor`
  - 起動時設定エラー時にこのページにリダイレクト
  - メッセージ: "システムが正しく構成されていません。管理者に連絡してください"
  - エラーログへのリンク（開発環境のみ）
  - 設定ファイルの例（引用）を記載して復旧を支援

### ドキュメント（US3）

- [ ] T070 [US3] README.md 更新: リポジトリルートの `README.md` に設定管理の概要を追記
  - User Secrets の使い方
  - 環境変数の一覧
  - Azure Key Vault との統合手順（quickstart.md へのリンク）

**Checkpoint**: User Story 3 が動作。管理者が設定を変更すると、再起動後に新しい設定が適用される。設定不備時は起動時にエラーが検出される。

---

## Phase 6: Polish & Cross-Cutting Concerns（最終仕上げ）

**Purpose**: 複数 User Story に影響する改善

### ヘルスチェック

- [ ] T071 [P] /health エンドポイント実装: `src/BlogDraftWebApp.Api/Endpoints/HealthEndpoints.cs`（単純な稼働状態確認、設定検証は行わない）

### セキュリティ強化

- [ ] T072 秘匿情報のログ除外確認: 全ログ出力箇所で API キー、エンドポイント URL、文体カードが記録されていないことを確認
- [ ] T073 HTTPS リダイレクト設定: `src/BlogDraftWebApp/Program.cs` で UseHttpsRedirection() を有効化
- [ ] T074 HSTS ヘッダ設定: `Program.cs` で UseHsts() を本番環境でのみ有効化

### パフォーマンス

- [ ] T075 [P] 文体カードのキャッシュ: `StyleCardOptions` を Singleton で登録し、起動時に 1 回だけ読み込む

### ドキュメント

- [ ] T076 [P] README.md 完成: リポジトリルートの `README.md` にプロジェクト概要、クイックスタート、ライセンスを記載
- [ ] T077 [P] CONTRIBUTING.md 作成: 開発者向けに、ビルド手順、テスト実行、PR ルールを記載

### 統合テスト

- [ ] T078 End-to-End テスト: `tests/BlogDraftWebApp.Api.IntegrationTests/E2ETests.cs`
  - 概要入力 → /draft 呼び出し → 下書き生成までの全体フローを検証（LLM API はモック）
  - プレビューモード → /draft/preview 呼び出し → プロンプト全文取得を検証

### CI/CD 準備

- [ ] T079 GitHub Actions ワークフロー作成: `.github/workflows/ci.yml`
  - dotnet build
  - dotnet test（UnitTests + IntegrationTests）
  - コードカバレッジレポート（オプション）
- [ ] T080 Docker サポート: リポジトリルートに `Dockerfile` 作成（quickstart.md の例を基に）

### Quickstart 検証

- [ ] T081 quickstart.md の手順を実行し、すべてのステップが動作することを確認

**Checkpoint**: すべての User Story が完成し、本番環境へのデプロイ準備が整った。

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: 依存なし - 即座に開始可能
- **Foundational (Phase 2)**: Setup 完了後 - **全 User Story をブロック**
- **User Stories (Phase 3-5)**: Foundational 完了後に開始可能
  - 複数人で並列実行可能（US1, US2, US3 は互いに独立）
  - または優先度順に順次実行（P1 → P2 → P3）
- **Polish (Phase 6)**: 実装したい User Story がすべて完了後

### User Story Dependencies

- **User Story 1 (P1)**: Foundational 完了後に開始可能 - 他ストーリーへの依存なし
- **User Story 2 (P2)**: Foundational 完了後に開始可能 - US1 のプロンプト合成ロジックを再利用するが、独立してテスト可能
- **User Story 3 (P3)**: Foundational 完了後に開始可能 - US1/US2 への依存なし（設定管理のみ）

### Within Each User Story

- モデル（T026-T030）はサービスより先に完了
- サービス（T031-T041）はエンドポイントより先に完了
- エンドポイント（T042-T044）は UI より先に完了
- UnitTests（T051-T053）は並列実行可能

### Parallel Opportunities

- **Phase 1 (Setup)**: T002-T007 は並列実行可能（異なるプロジェクトを作成）
- **Phase 2 (Foundational)**: T013-T015（設定モデル）、T019-T023（ログ・エラー基盤）は並列実行可能
- **Phase 3 (US1)**: T026-T030（モデル）、T051-T053（UnitTests）は並列実行可能
- **Phase 6 (Polish)**: T071, T075, T076-T077 は並列実行可能

---

## Parallel Example: User Story 1

```bash
# 並列実行可能なタスク（異なるファイル）:
Task T026: BlogOverview モデル作成
Task T027: StyleCard モデル作成
Task T028: RAGChunk モデル作成
Task T029: Prompt モデル作成
Task T030: Draft モデル作成

# モデル完成後、サービスを並列実行:
Task T031-T034: RAG サービス（AzureAISearchService.cs）
Task T035-T037: LLM クライアント（OpenAIClient.cs）
Task T038-T041: プロンプト合成（PromptComposer.cs）

# サービス完成後、テストを並列実行:
Task T051: PromptComposer テスト
Task T052: AzureAISearchService テスト
Task T053: OpenAIClient テスト
```

---

## Implementation Strategy

### MVP First (User Story 1 のみ)

1. Phase 1 (Setup) を完了
2. Phase 2 (Foundational) を完了 → **基盤完成**
3. Phase 3 (User Story 1) を完了 → **MVP 完成！**
4. **ここで停止して検証**: 下書き生成が動作することを確認
5. 必要に応じてデプロイ・デモ

### Incremental Delivery

1. Setup + Foundational → 基盤完成
2. User Story 1 → 独立テスト → デプロイ/デモ（**MVP!**）
3. User Story 2 → 独立テスト → デプロイ/デモ（プレビュー機能追加）
4. User Story 3 → 独立テスト → デプロイ/デモ（設定管理強化）
5. 各ストーリーが既存機能を壊さずに価値を追加

### Parallel Team Strategy

複数開発者がいる場合:

1. 全員で Setup + Foundational を完了
2. Foundational 完了後、並列実行:
   - 開発者 A: User Story 1
   - 開発者 B: User Story 2
   - 開発者 C: User Story 3
3. 各ストーリーが独立して完成・統合

---

## Summary

- **Total Tasks**: 81
- **Task Count by User Story**:
  - Setup: 12 tasks
  - Foundational: 13 tasks（**ブロッキング**）
  - User Story 1 (P1): 27 tasks（**MVP**）
  - User Story 2 (P2): 9 tasks
  - User Story 3 (P3): 8 tasks
  - Polish: 12 tasks

- **Parallel Opportunities**: Setup（6 タスク）、Foundational（5 タスク）、US1 モデル（5 タスク）、US1 テスト（3 タスク）等で並列実行可能

- **Independent Test Criteria**:
  - **US1**: 概要入力 → 下書き生成 → コピー可能
  - **US2**: プレビューモード → プロンプト全文表示 → LLM API 呼び出しなし
  - **US3**: 設定変更 → 再起動 → 新設定適用

- **MVP Scope**: User Story 1 のみ（Setup + Foundational + US1 = 52 tasks）

---

## Notes

- **[P]** = 並列実行可能（異なるファイル、依存なし）
- **[Story]** = タスクが属する User Story（トレーサビリティ用）
- 各 User Story は独立して完成・テスト可能
- Checkpoint で各 Story の動作を検証
- 曖昧なタスクを避け、具体的なファイルパスを含む
- 同一ファイルの競合を避ける
- Story 間の依存を最小化

---

**Tasks Status**: ✅ GENERATED  
**Branch**: `001-blog-draft-webapp`  
**Tasks Path**: `D:\Data\git\blog_draft_webapp\specs\001-blog-draft-webapp\tasks.md`
