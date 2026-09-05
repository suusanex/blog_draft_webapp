# ブログ下書き生成 Web アプリ

WinUI 3 デスクトップアプリで提供していたブログ下書き生成機能を、Blazor Web App（Interactive Server）として再実装したものです。生成は文体カードとユーザー入力を使い、入力の内容と論理展開を保ち、必要な補足だけを加えます。過去記事の RAG 検索は生成に使いません。

## できること

- 記事の概要（原稿のラフ）を入力して Markdown の下書きを生成（文体カード + LLM）
- 「プレビューモード」で LLM に送るプロンプト全文を表示（LLM は呼び出しません）
- ワークフロー型生成（アウトライン → 下書き）。タイトル＆導入部は本文のみを入力とする独立機能
- セッション再開・一覧・削除（`/workflow/*`）
- `/health` で稼働確認（設定検証はしません）

## 技術スタック

- C# / .NET 10
- Blazor Web App（Interactive Server）
- ASP.NET Core Minimal API（/draft, /draft/preview, /workflow/*, /health）

## クイックスタート（ローカル）

詳細は [specs/001-blog-draft-webapp/quickstart.md](specs/001-blog-draft-webapp/quickstart.md) を参照してください。

最小手順:

```powershell
cd src/BlogDraftWebApp
dotnet build
dotnet run
```

## 設定（管理者向け）

秘匿情報（API キー、文体カード）はリポジトリに含めません。以下のいずれかで設定してください。

- 開発: User Secrets
- 運用: 環境変数（Azure App Service の Application Settings など）
- 運用: Azure Key Vault（`KeyVault:VaultUri` を設定）

### 必須（LLM / 文体カード）

- `OpenAI:ApiKey`
- `OpenAI:Model`
- `StyleCard:SystemPrompt`
- `StyleCard:Content`

環境変数で設定する場合は `:` を `__` に置き換えます。

例:

- `OpenAI__ApiKey`
- `OpenAI__Model`
- `StyleCard__SystemPrompt`
- `StyleCard__Content`

### 文体カードの適用

編集方針の一般テンプレートは [templates/editorial-policy.md](templates/editorial-policy.md) を参照してください。個人用カード全文はリポジトリに含めません。

カードや `StyleCard:SystemPrompt` を変えたあとはアプリを再起動し、プレビューで改訂内容が送信対象になっていることを確認してください。起動時に Singleton として読むため、ファイル保存だけでは未適用です。

### Azure AI Search（生成には未使用）

下書き生成・プレビュー・ワークフロー生成は Azure AI Search を呼びません。`AzureAISearch:Enabled=true` でも、LLM へ送るメッセージに過去記事を入れません。

旧ワークフロー API `POST /workflow/sessions/{id}/rag/refresh` とその confirm は互換のため残していますが、非推奨です。呼び出しても生成プロンプトへ再注入しません。Azure 資源の削除は不要です。

評価用の入力例は [evals/issue-12/](evals/issue-12/) にあります。

### 設定不備時の挙動

- 起動時に設定を検証し、不備があればログにエラーを出力します。
- UI は `Configuration Error` 画面へ誘導されます。
- API（`/draft`, `/draft/preview`）は `CONFIG_ERROR` を返します。
- `/health` は設定検証を行わず 200 を返します。

## テスト

```powershell
dotnet test -c Release
```

## ライセンス

このリポジトリは [LICENSE](LICENSE) を参照してください。
