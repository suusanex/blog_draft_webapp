# テスト実装要件追加 - 完了サマリー

**日付**: 2026-01-24  
**作業内容**: テストカバレッジ分析を踏まえ、全機能に対するテスト実装要件をドキュメントに明記

---

## 📝 実施した変更

### 1. spec.md の更新

#### 追加: テスト要件セクション（Testing Requirements）

- **TR-001**: UI コンポーネントの bUnit テスト（必須）
- **TR-002**: API エンドポイントの統合テスト（必須）
- **TR-003**: コアビジネスロジックのユニットテスト（必須）
- **TR-004**: 主要ユーザーフローの Playwright E2E テスト（必須）
- **TR-005**: 外部依存のモック化、実 OS 環境変更禁止
- **TR-006**: Assert.That 形式の使用
- **TR-007**: UI テストでの依存モック化
- **TR-008**: E2E テストでのブラウザ操作検証
- **TR-009**: カバレッジ目標（行 80%、分岐 70%）
- **TR-010**: 新機能追加時のテスト同時実装

#### 追加: Success Criteria

- **SC-009**: テストカバレッジ目標の達成
- **SC-010**: E2E テスト 10 シナリオ以上
- **SC-011**: CI での自動テスト実行とビルド失敗
- **SC-012**: 新機能追加時のテストカバレッジ維持

**ファイル**: [specs/001-blog-draft-webapp/spec.md](specs/001-blog-draft-webapp/spec.md)

---

### 2. plan.md の更新

#### 追加: Testing Strategy セクション

**テストピラミッド構造**:
```
         E2E（Playwright）
        統合テスト（WebApplicationFactory）
    ユニットテスト（NUnit + Moq / bUnit）
```

**3層のテスト戦略**:
1. **ユニットテスト**: コアロジック（NUnit + Moq）、UI ロジック（bUnit）
2. **統合テスト**: API エンドポイント（WebApplicationFactory + Moq）
3. **E2E テスト**: ユーザーフロー（Playwright + NUnit）

#### 追加: プロジェクト構造

**新規テストプロジェクト**:
- `BlogDraftWebApp.Components.Tests/` - bUnit による Blazor コンポーネントテスト
- `BlogDraftWebApp.E2E.Tests/` - Playwright による E2E テスト

**テスト対象の詳細**:
- GenerateDraft.razor の全ロジック（状態管理、入力検証、非同期処理、JS インターロップ）
- API エンドポイント（/draft, /draft/preview, /health）
- コアサービス（PromptComposer, AzureAISearchService, OpenAIClient）

#### 追加: Key Decisions にテスト戦略を追加

| 項目 | 決定内容 |
|------|---------|
| テスト戦略 | 3層テストピラミッド（Unit/Integration/E2E） |
| Unit テスト | NUnit + Moq（Core）、bUnit（Blazor） |
| Integration テスト | WebApplicationFactory + Moq |
| E2E テスト | Playwright + NUnit |
| カバレッジ目標 | 行 80%、分岐 70%、E2E 10 シナリオ |

**ファイル**: [specs/001-blog-draft-webapp/plan.md](specs/001-blog-draft-webapp/plan.md)

---

### 3. TEST_COVERAGE_ANALYSIS.md の更新

- レポート冒頭にドキュメント更新の事実を追記
- spec.md と plan.md へのリンクを追加
- 更新日を明記

**ファイル**: [TEST_COVERAGE_ANALYSIS.md](TEST_COVERAGE_ANALYSIS.md)

---

## 🎯 定義されたテスト実装範囲

### bUnit テスト（未実装 → 実装必須）

**対象コンポーネント**: GenerateDraft.razor

| カテゴリ | テスト項目 | 必須度 |
|---------|----------|--------|
| 入力・検証 | `OnOverviewChanged`, 最小/最大文字数検証、ボタン無効化ロジック | 🔴 必須 |
| 非同期処理 | `GenerateAsync()`, `PreviewAsync()`, `RetryAsync()` | 🔴 必須 |
| UI 表示制御 | エラー、警告、結果パネルの条件付き表示 | 🔴 必須 |
| 副作用 | Markdown 変換、JavaScript インターロップ（コピー） | 🔴 必須 |

**テスト数**: 最低 20 テストケース以上

---

### Playwright E2E テスト（未実装 → 実装必須）

**対象フロー**: エンドツーエンドのユーザー操作

| カテゴリ | テストシナリオ | 必須度 |
|---------|--------------|--------|
| 正常系 | 入力 → 生成 → 表示 → コピー | 🔴 必須 |
| | 入力 → プレビュー → 表示 → コピー | 🔴 必須 |
| 入力検証 | 空文字列でボタン無効、文字数エラー | 🔴 必須 |
| エラー処理 | API エラー表示、リトライ動作 | 🔴 必須 |
| UI 状態 | ローディング表示、状態リセット | 🔴 必須 |

**テスト数**: 最低 10 シナリオ以上

---

## 📊 目標達成基準

### コードカバレッジ

| メトリクス | 目標値 | 測定ツール |
|----------|--------|-----------|
| 行カバレッジ | **80% 以上** | coverlet.collector |
| 分岐カバレッジ | **70% 以上** | coverlet.collector |
| E2E シナリオ | **10 以上** | Playwright テスト数 |

### CI/CD 統合

- ✅ すべてのテストが CI で自動実行される
- ✅ テスト失敗時はビルドが失敗する
- ✅ カバレッジレポートが自動生成される
- ✅ 新機能追加時はテスト同時実装が必須

---

## 🚀 次のステップ

### Phase 3: bUnit テスト実装

1. **新規テストプロジェクト作成**:
   ```
   tests/BlogDraftWebApp.Components.Tests/BlogDraftWebApp.Components.Tests.csproj
   ```

2. **NuGet パッケージ追加**:
   - `bunit`
   - `bunit.web`
   - NUnit, Moq, coverlet.collector

3. **テストファイル作成**:
   - `Pages/GenerateDraftTests.cs`
   - `Mocks/MockHttpClientFactory.cs`
   - `Mocks/MockJSRuntime.cs`
   - `Mocks/MockNavigationManager.cs`

4. **実装**:
   - 全ロジックのユニットテスト
   - モック化された依存の注入
   - Assert.That 形式での検証

### Phase 4: Playwright E2E テスト実装

1. **新規テストプロジェクト作成**:
   ```
   tests/BlogDraftWebApp.E2E.Tests/BlogDraftWebApp.E2E.Tests.csproj
   ```

2. **NuGet パッケージ追加**:
   - `Microsoft.Playwright`
   - `Microsoft.Playwright.NUnit`

3. **テストファイル作成**:
   - `Pages/GenerateDraftPageTests.cs`
   - `Fixtures/BrowserFixture.cs`
   - `appsettings.e2e.json`

4. **実装**:
   - ユーザーフローの自動化
   - ブラウザ操作の検証
   - スクリーンショット取得（失敗時）

---

## 📚 参考ドキュメント

- [spec.md - テスト要件（TR-001 ～ TR-010）](specs/001-blog-draft-webapp/spec.md#テスト要件testing-requirements)
- [spec.md - Success Criteria（SC-009 ～ SC-012）](specs/001-blog-draft-webapp/spec.md#measurable-outcomes)
- [plan.md - Testing Strategy](specs/001-blog-draft-webapp/plan.md#testing-strategy)
- [plan.md - Project Structure（テストプロジェクト）](specs/001-blog-draft-webapp/plan.md#project-structure)
- [TEST_COVERAGE_ANALYSIS.md - テストカバレッジ分析](TEST_COVERAGE_ANALYSIS.md)

---

**ステータス**: ✅ ドキュメント更新完了  
**次のアクション**: Phase 3（bUnit テスト実装）の開始
