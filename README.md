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
- `StyleCard:Content` または `StyleCard:FilePath`

`StyleCard:SystemPrompt` は任意です。指定した場合は文体カードとともに LLM へ渡します。

環境変数で設定する場合は `:` を `__` に置き換えます。

例:

- `OpenAI__ApiKey`
- `OpenAI__Model`
- `StyleCard__Content`
- `StyleCard__FilePath`

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

実行エラーの調査でスタックトレースが必要な場合は、`run` または `compare` に `--verbose` を追加します。詳細出力にはローカルパスなどが含まれる可能性があるため、共有ログや CI では必要な場合にだけ使用してください。

Issue #1 の変更前後など、2つの実行結果を比較する場合:

```powershell
dotnet run --project src/BlogDraftWebApp.QualityEvaluation -- compare `
  --baseline artifacts/quality-evaluation/BEFORE/run-report.json `
  --candidate artifacts/quality-evaluation/AFTER/run-report.json
```

### 評価結果の読み方

まず `run-report.md` の各ケースの生成本文を読み、次に JSON / Markdown の指標で傾向を確認します。指標は人手レビューの優先順位を付けるためのものであり、単独で記事の良否を決めるものではありません。

`run-report.md` の Results 表では、文字数、見出し数、箇条書き数、コードブロック数、中心ポイント被覆率、補足トピック被覆率、禁止範囲候補を一覧できます。RAG deltas 表と `comparison-report.md` では、中心ポイントと補足トピックの両方について差分を確認できます。より詳細なトピック別判定や構成違反は JSON レポートを参照してください。

1. **実行範囲を確認する**: `Results` に `RAG disabled` と `RAG enabled` の両方があり、`RAG deltas` がケース数分あるかを確認します。enabled が skip されている、または disabled だけを実行した場合、そのレポートはプロンプト単体の基準値であり、RAG の効果・副作用は判断できません。
2. **中心ポイントの被覆を見る**: `Focal coverage` は、各ケースの `expectedFocalPoints.anyOf` にある語句が本文へ現れた割合です。低いケースは、中心ポイントの見落とし、言い換えによる測定上の取りこぼし、またはケース側のキーワード不足のいずれかです。本文を読み、同じ種類のポイントが繰り返し抜けている場合を改善候補にします。
3. **必要な補足を確認する**: `Supporting coverage` が低い場合、入力の結論を理解するための原因・比較条件・限定範囲が省かれている可能性があります。中心ポイントを繰り返すだけの記事になっていないかを確認します。
4. **長さと構成を確認する**: `Chars` が `targetLengthRange` 外なら、短すぎて説明不足か、長すぎて不要な一般論が増えたかを本文で判断します。`Headings` が上限内で、`Forbidden candidates` と `Generic structure overuse` がないことは、過剰な網羅記事へ寄っていないことを示す補助的な良い兆候です。目標文字数はケースごとの目安であり、すべてのケースが同じ方向へ外れる場合はプロンプトだけでなくケースの範囲設定も見直します。
5. **RAG の差分を見る**: `RAG deltas` は enabled から disabled を引いた値です。文字数・見出し数の大幅な正の差、新しい禁止範囲候補、中心ポイントまたは補足トピック被覆の低下は、RAG が不要な情報を増やしている疑いがあります。逆に、補足や中心ポイントの被覆が上がり、長さや禁止範囲が悪化していなければ RAG の効果を確認できます。
6. **変更前後を比較する**: `comparison-report.md` の値は candidate から baseline を引いた差です。文字数・見出し数が減っても、被覆率や必要な補足が維持・改善されているかを確認します。新しい禁止範囲候補は優先して本文をレビューします。

判断例として、「禁止範囲候補がなく、見出し数も上限内だが、中心・補足ポイントの被覆が低く、文字数も下限未満」という結果は、一般論を増やす必要はない一方で、入力の結論を支える説明が不足している可能性を示します。

評価結果は既定で `artifacts/quality-evaluation/` に JSON と Markdown で出力され、このディレクトリは Git 管理対象外です。生成本文は人手レビュー用に保存されますが、API キー、プロンプト全文、文体カード、RAG 本文・URL・タイトルは保存されません。生成本文に文体カードや RAG 情報の完全一致、または16文字以上の連続する部分引用が含まれる場合は、保存前にマスキングされます。RAG 出典は不可逆な SHA-256 識別子としてのみ記録されます。実ユーザーの入力を固定ケースへ追加しないでください。

## ライセンス

このリポジトリは [LICENSE](LICENSE) を参照してください。
