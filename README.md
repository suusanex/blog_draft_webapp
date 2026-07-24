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

## 生成品質の固定評価

`evaluation/cases.json` にある匿名化済みの固定ケースを使い、出力長、Markdown の見出し・箇条書き・コードブロック数、期待ポイントの被覆、禁止範囲の候補を計測できます。

外部サービスを呼ばないスタブ評価:

```powershell
dotnet run --project src/BlogDraftWebApp.QualityEvaluation -- run --mode stub --prompt-version local-stub --rag both
```

実 LLM と Azure AI Search を使った RAG 有無の比較:

```powershell
dotnet run --project src/BlogDraftWebApp.QualityEvaluation -- run --mode live --prompt-version issue-1-before --rag both
```

live モードは Web アプリと同じ `appsettings.json`、Development User Secrets、環境変数、Azure Key Vault、コマンドライン引数の順で設定を読み込みます。評価ツール固有の引数の後へ、`--OpenAI:Model MODEL_NAME` などの ASP.NET Core 構成引数を指定できます。OpenAI API キーがない場合は外部接続せず skip し、RAG 設定がない場合は RAG enabled 側だけを skip します。

Issue #1 の変更前後など、2つの実行結果を比較する場合:

```powershell
dotnet run --project src/BlogDraftWebApp.QualityEvaluation -- compare `
  --baseline artifacts/quality-evaluation/BEFORE/run-report.json `
  --candidate artifacts/quality-evaluation/AFTER/run-report.json
```

評価結果は既定で `artifacts/quality-evaluation/` に JSON と Markdown で出力され、このディレクトリは Git 管理対象外です。生成本文は人手レビュー用に保存されますが、API キー、プロンプト全文、文体カード、RAG 本文・URL・タイトルは保存されません。RAG 出典は不可逆な SHA-256 識別子としてのみ記録されます。実ユーザーの入力を固定ケースへ追加しないでください。

## ライセンス

このリポジトリは [LICENSE](LICENSE) を参照してください。
