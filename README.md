# ブログ下書き生成 Web アプリ

WinUI 3 デスクトップアプリで提供していた「ブログ下書き生成（RAG＋文体カード＋LLM）」機能を、Blazor Web App（Interactive Server）として再実装したものです。

## できること

- 記事の概要を入力して Markdown の下書きを生成（RAG + 文体カード + LLM）
- 「プレビューモード」で LLM に送るプロンプト全文を表示（LLM は呼び出しません）
- `/health` で稼働確認（設定検証はしません）

## 技術スタック

- C# / .NET 10
- Blazor Web App（Interactive Server）
- ASP.NET Core Minimal API（/draft, /draft/preview, /health）

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

### 任意（RAG: Azure AI Search）

RAG を有効化する場合:

- `AzureAISearch:Enabled=true`
- `AzureAISearch:Endpoint`
- `AzureAISearch:ApiKey`
- `AzureAISearch:IndexName`
- `AzureAISearch:TextFieldName`
- `AzureAISearch:VectorFieldName`

RAG を無効化する場合:

- `AzureAISearch:Enabled=false`

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
