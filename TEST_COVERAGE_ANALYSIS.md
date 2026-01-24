# GenerateDraft.razor テストカバレッジ分析レポート

**⚠️ 更新: 2026-01-24 - テスト要件がドキュメントに追加されました**

このレポートは初回分析時点（2026-01-24）のテスト実装状況を示しています。

**📋 ドキュメント更新内容:**
- ✅ [spec.md](specs/001-blog-draft-webapp/spec.md) にテスト要件（TR-001 ～ TR-010）を追加
- ✅ [spec.md](specs/001-blog-draft-webapp/spec.md) に Success Criteria（SC-009 ～ SC-012）を追加
- ✅ [plan.md](specs/001-blog-draft-webapp/plan.md) にテスト戦略とテストピラミッド構造を追加
- ✅ [plan.md](specs/001-blog-draft-webapp/plan.md) にプロジェクト構造（bUnit/Playwright テストプロジェクト）を追加

## 📋 調査対象
- **コンポーネント**: `src/BlogDraftWebApp/Components/Pages/GenerateDraft.razor`
- **調査対象フレームワーク**: 
  - bUnit（Blazorコンポーネント単体テスト）
  - Playwright（E2Eテスト）

---

## 🔍 現状分析

### 1. bUnit テストの実装状況

#### ❌ **実装されていない**

`GenerateDraft.razor` コンポーネント専用の bUnit テストは **完全に存在しません**。

**テストプロジェクトの構成:**
- `BlogDraftWebApp.Core.UnitTests/` - Core層のユニットテストのみ
- `BlogDraftWebApp.Api.IntegrationTests/` - API エンドポイントの統合テストのみ
- Blazor コンポーネント用のテストプロジェクトなし

**プロジェクト参照確認:**
- `BlogDraftWebApp.Api.IntegrationTests.csproj` に bUnit パッケージの参照なし
- `BlogDraftWebApp.Core.UnitTests.csproj` に bUnit パッケージの参照なし

---

### 2. Playwright E2E テストの実装状況

#### ⚠️ **未実装**

Playwright による E2E テストは **実装されていません**。

**テストプロジェクトファイルの確認:**
- `E2ETests.cs` は実際のこのファイル名がありますが、内容を確認すると：
  - これは **API エンドポイントのテスト** であり、Playwright ではなく `WebApplicationFactory` を使用
  - HTTPClient で API を直接呼び出すテストのみ
  - ブラウザ操作や UI の自動化テストなし

**Playwright パッケージの確認:**
- プロジェクトファイルに `Microsoft.Playwright` パッケージの参照なし
- Playwright テストプロジェクトの設定なし

---

## 📊 テストカバレッジ詳細

### ✅ 実装されているもの

#### **API エンドポイントテスト（`DraftEndpoints.cs` に対応）**
- ✅ `/draft/preview` エンドポイントの統合テスト
  - `DraftEndpointsTests.cs`: プレビュー機能が LLM を呼び出さないことを確認
  - `E2ETests.cs`: プレビュー機能のエンドツーエンドテスト

- ✅ `/draft` エンドポイントの統合テスト
  - `E2ETests.cs`: 下書き生成が正常に動作することを確認
  - `E2ETests.cs`: RAG チャンク数の検証
  - `E2ETests.cs`: エラーハンドリング

#### **ビジネスロジックテスト（`PromptComposer`）**
- ✅ `PromptComposerTests.cs`: プロンプト合成ロジック
  - RAG チャンク有りの場合の合成
  - RAG チャンク無しの場合の合成

---

### ❌ 実装されていないもの

#### **1. bUnit によるコンポーネントロジックテスト**

以下の機能は bUnit でテストされていません：

| 機能 | テスト項目 | 状態 |
|------|----------|------|
| **フォーム入力** | 入力値変更時の状態更新（`OnOverviewChanged`） | ❌ |
| **入力値検証** | 最小文字数（10文字）以上の検証 | ❌ |
| | 最大文字数（5000文字）以下の検証 | ❌ |
| | 空文字列の拒否 | ❌ |
| **ボタン状態** | `IsGenerateDisabled` プロパティの判定ロジック | ❌ |
| | 入力値が空の場合にボタン無効化 | ❌ |
| | 処理中（`_isBusy`）にボタン無効化 | ❌ |
| **生成処理** | `GenerateAsync()` - 正常系（成功） | ❌ |
| | `GenerateAsync()` - エラー時の処理 | ❌ |
| | `GenerateAsync()` - リトライ可能なエラー表示 | ❌ |
| | `GenerateAsync()` - 生成中状態の管理 | ❌ |
| **プレビュー処理** | `PreviewAsync()` - 正常系（成功） | ❌ |
| | `PreviewAsync()` - エラー時の処理 | ❌ |
| | `PreviewAsync()` - LLM を呼び出さないこと | ❌ |
| **状態リセット** | エラー時に前の結果をクリア | ❌ |
| | 新しい処理開始時に状態をリセット | ❌ |
| **Markdown 処理** | `Markdig.ToHtml()` による HTML 変換 | ❌ |
| | HTML 出力のサニタイズ（`DisableHtml()`） | ❌ |
| **クリップボード操作** | `CopyDraftAsync()` - JSインターロップ呼び出し | ❌ |
| | `CopyPromptAsync()` - JSインターロップ呼び出し | ❌ |
| | コピー後のメッセージ表示 | ❌ |
| **UI 表示制御** | エラーメッセージの条件付き表示 | ❌ |
| | 警告メッセージの条件付き表示 | ❌ |
| | プレビューパネルの表示/非表示 | ❌ |
| | 生成結果パネルの表示/非表示 | ❌ |
| **警告表示** | 短い生成結果の警告（100文字未満） | ❌ |
| | RAG 警告メッセージの表示 | ❌ |
| **リトライ機能** | `RetryAsync()` による再生成 | ❌ |

#### **2. Playwright による E2E テスト**

以下の機能は Playwright でテストされていません：

| テストシナリオ | 説明 | 状態 |
|-------------|------|------|
| **ユーザーフロー - 正常系** | テキスト入力 → プレビューボタン → プレビュー表示 | ❌ |
| | テキスト入力 → 生成ボタン → 下書き表示 | ❌ |
| | クリップボードコピー動作確認 | ❌ |
| **ユーザーフロー - 入力値エラー** | 空文字列状態でボタンが無効か | ❌ |
| | 9文字入力でエラー表示 | ❌ |
| | 5001文字入力でエラー表示 | ❌ |
| **ユーザーフロー - API エラー** | API エラー時のエラーメッセージ表示 | ❌ |
| | リトライボタンが表示される（リトライ可能な場合） | ❌ |
| | リトライボタンをクリックして再生成 | ❌ |
| **UI インタラクション** | プレビューモードチェックボックスの動作 | ❌ |
| | 生成中のローディング表示 | ❌ |
| | Markdown HTML のレンダリング | ❌ |
| **ブラウザ機能連携** | クリップボードへのコピー成功メッセージ | ❌ |
| | クリップボードコピーの実際の動作 | ❌ |
| **レスポンシブ性** | モバイルビューでの UI 表示 | ❌ |
| **エラー回復** | ネットワークエラー後のリトライ | ❌ |
| **パフォーマンス** | 大量テキスト入力時のレスポンス | ❌ |

---

## 📈 現在のテストピラミッド

```
                    ╱╲
                   ╱  ╲         E2E テスト
                  ╱____╲        ❌ 0%（Playwright）
                 ╱      ╲
                ╱        ╲       統合テスト
               ╱  API     ╲      ✅ 部分的（API エンドポイントのみ）
              ╱            ╲
             ╱              ╲
            ╱________________╲   ユニットテスト
           ╱ Core Logic Tests  ╲  ⚠️ 限定的（Core層のみ）
          ╱                    ╲ 
         ╱______________________╲ ❌ 0%（Blazor コンポーネント）
```

---

## 🎯 テスト実装に必要な構成

### bUnit テスト導入に必要なセットアップ

1. **新規テストプロジェクト作成（推奨）**
   ```
   tests/BlogDraftWebApp.Components.Tests/
   BlogDraftWebApp.Components.Tests.csproj
   ```

2. **NuGet パッケージ追加**
   - `bunit` (最新版)
   - `bunit.web` 
   - 既存の NUnit, Moq, coverlet.collector も追加

3. **テストファイル配置構造**
   ```
   tests/BlogDraftWebApp.Components.Tests/
   ├── Pages/
   │   └── GenerateDraftTests.cs
   ├── Mocks/
   │   ├── MockHttpClientFactory.cs
   │   ├── MockJSRuntime.cs
   │   └── MockNavigationManager.cs
   └── Fixtures/
       └── GenerateDraftFixture.cs
   ```

### Playwright E2E テスト導入に必要なセットアップ

1. **新規テストプロジェクト作成（推奨）**
   ```
   tests/BlogDraftWebApp.E2E.Tests/
   BlogDraftWebApp.E2E.Tests.csproj
   ```

2. **NuGet パッケージ追加**
   - `Microsoft.Playwright`
   - `Microsoft.Playwright.NUnit`

3. **テストファイル配置構造**
   ```
   tests/BlogDraftWebApp.E2E.Tests/
   ├── Pages/
   │   └── GenerateDraftPageTests.cs
   ├── Fixtures/
   │   └── BrowserFixture.cs
   └── appsettings.e2e.json
   ```

---

## ⚠️ テスト未実装による影響

### 回帰テストリスク（🔴 高）
- コンポーネントロジック変更時に意図しない動作変更を検出できない
- JavaScript インターロップ処理のバグが実装時に検出されない

### メンテナンスリスク（🔴 高）
- 今後の機能追加時に既存機能の破壊を防ぐ手段がない
- 複雑な状態管理（エラー、ローディング、入力値）のテストがない

### ユーザー体験リスク（🔴 中）
- UI から API までの実際のユーザーフローが検証されていない
- ネットワークエラーやタイミング問題が検出されていない

---

## 📌 現在のテスト実装状況の概要

| テストレベル | フレームワーク | 実装状況 | カバレッジ |
|------------|--------------|--------|---------|
| **E2E** | Playwright | ❌ 未実装 | 0% |
| **統合** | HTTP Client | ✅ 部分実装 | API エンドポイントのみ |
| **ユニット** | bUnit | ❌ 未実装 | 0% |
| | NUnit (Core) | ✅ 部分実装 | PromptComposer のみ |

---

## 📋 結論

### bUnit ロジックテスト: **❌ 未実装**
`GenerateDraft.razor` コンポーネントのロジックを検証するための bUnit テストは **存在しません**。コンポーネント内のすべてのメソッド、状態管理、UI ロジックがテストされていません。

### Playwright E2E テスト: **❌ 未実装**
ユーザーが実際に操作する UI 層の E2E テストは実装されていません。API エンドポイントレベルの統合テストは存在しますが、Playwright によるブラウザ自動化テストはありません。

---

*レポート生成日: 2026-01-24*
