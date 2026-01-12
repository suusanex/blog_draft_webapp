# Quickstart: ブログ下書き生成 Web アプリケーション

**Branch**: `001-blog-draft-webapp` | **Date**: 2026-01-11  
**Purpose**: Phase 1 - 開発者向けセットアップ手順

## Overview

このドキュメントは、ブログ下書き生成 Web アプリケーションをローカル開発環境でセットアップし、動作確認するための手順を説明する。

---

## 前提条件

### 必須

- **.NET 10 SDK**: [ダウンロード](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Visual Studio Code** または **Visual Studio 2022**
- **Azure AI Search インスタンス**: RAG 機能を使用する場合（オプション）
- **OpenAI API キー**: LLM 生成に必要（または OpenAI 互換 API）

### オプション

- **Azure Key Vault**: 本番環境での秘匿情報管理用
- **Docker Desktop**: コンテナでの実行を試す場合

---

## 1. リポジトリのクローン

```powershell
git clone https://github.com/your-org/blog-draft-webapp.git
cd blog-draft-webapp
git checkout 001-blog-draft-webapp
```

---

## 2. 秘匿情報の設定（User Secrets）

### 2.1 User Secrets ID の初期化

```powershell
cd src/BlogDraftWebApp
dotnet user-secrets init
```

### 2.2 LLM API キーの設定

```powershell
# OpenAI の場合
dotnet user-secrets set "OpenAI:ApiKey" "sk-YOUR_OPENAI_API_KEY"
dotnet user-secrets set "OpenAI:Model" "gpt-4o"
dotnet user-secrets set "OpenAI:BaseUrl" "https://api.openai.com/v1"

# Azure OpenAI の場合
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_AZURE_OPENAI_KEY"
dotnet user-secrets set "OpenAI:Model" "gpt-4"
dotnet user-secrets set "OpenAI:BaseUrl" "https://YOUR_RESOURCE.openai.azure.com/"
```

### 2.3 文体カードの設定

文体カードの内容をファイルとして保存し、そのパスを設定する方法:

```powershell
# 文体カードをファイルに保存（例: C:\secrets\stylecard.md）
# その後、パスを User Secrets に設定
dotnet user-secrets set "StyleCard:FilePath" "C:\secrets\stylecard.md"

# または、直接 User Secrets に記載（短い場合）
dotnet user-secrets set "StyleCard:SystemPrompt" "あなたはテックブログの執筆アシスタントです。"
dotnet user-secrets set "StyleCard:Content" "## 執筆方針`n- 読者に寄り添う`n- 具体例を豊富に"
```

> **注意**: 文体カードは機微情報として扱うため、リポジトリにコミットしないこと。

### 2.4 RAG の設定（オプション）

RAG を使用する場合:

```powershell
dotnet user-secrets set "AzureAISearch:Enabled" "true"
dotnet user-secrets set "AzureAISearch:Endpoint" "https://YOUR_SEARCH_SERVICE.search.windows.net"
dotnet user-secrets set "AzureAISearch:ApiKey" "YOUR_SEARCH_API_KEY"
dotnet user-secrets set "AzureAISearch:IndexName" "blog-articles"
dotnet user-secrets set "AzureAISearch:TextFieldName" "content"
dotnet user-secrets set "AzureAISearch:VectorFieldName" "contentVector"
```

RAG を無効化する場合:

```powershell
dotnet user-secrets set "AzureAISearch:Enabled" "false"
```

---

## 3. アプリケーションのビルド

```powershell
cd src/BlogDraftWebApp
dotnet build
```

### エラーが発生した場合

- 依存パッケージが不足している場合、`dotnet restore` を実行。
- .NET SDK のバージョンを確認: `dotnet --version`（10.0 以降であること）。

---

## 4. アプリケーションの起動

```powershell
cd src/BlogDraftWebApp
dotnet run
```

### 期待される出力

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

ブラウザで [https://localhost:5001](https://localhost:5001) にアクセス。

---

## 5. 動作確認

### 5.1 下書き生成のテスト

1. トップページで「記事の概要」フォームに以下を入力:

   ```
   # タイトル案
   Azure Functions で Durable Functions を使った非同期処理
   
   # 想定読者
   Azure Functions 初心者
   
   # 目的
   長時間実行される処理を Durable Functions で実装する方法を解説
   
   # 主要な手順
   - Durable Functions のセットアップ
   - Orchestrator 関数の実装
   - Activity 関数の実装
   ```

2. 「下書きを生成」ボタンをクリック。
3. Markdown 形式の下書きが表示されることを確認。
4. 「コピー」ボタンをクリックし、クリップボードにコピーされることを確認。

### 5.2 プレビューモードのテスト

1. 「プレビューモード」チェックボックスを有効化。
2. 同じ概要を入力し、「プレビュー」ボタンをクリック。
3. LLM に送信される予定のプロンプト全文（文体カード + RAG コンテキスト + ユーザー概要）が表示されることを確認。
4. LLM API は呼び出されない（ログで確認可能）。

### 5.3 RAG フォールバックのテスト

1. User Secrets で RAG を無効化:
   ```powershell
   dotnet user-secrets set "AzureAISearch:Enabled" "false"
   ```
2. アプリを再起動し、下書きを生成。
3. 警告メッセージ「関連記事が見つかりませんでした」が表示され、生成が継続することを確認。

### 5.4 エラーハンドリングのテスト

#### API キー未設定

1. User Secrets から API キーを削除:
   ```powershell
   dotnet user-secrets remove "OpenAI:ApiKey"
   ```
2. アプリを再起動し、下書き生成を試行。
3. エラーメッセージ「システムが正しく構成されていません。管理者に連絡してください」が表示されることを確認。

#### タイムアウト

1. タイムアウト時間を短く設定:
   ```powershell
   dotnet user-secrets set "OpenAI:RequestTimeoutSeconds" "5"
   ```
2. 下書き生成を試行（5 秒で完了しないモデルを使用）。
3. エラーメッセージ「生成に時間がかかりすぎています。もう一度お試しください」が表示されることを確認。

---

## 6. テストの実行

### 6.1 単体テスト

```powershell
cd tests/BlogDraftWebApp.Core.UnitTests
dotnet test
```

### 6.2 統合テスト

```powershell
cd tests/BlogDraftWebApp.Api.IntegrationTests
dotnet test
```

### テスト結果の確認

```
Test Run Successful.
Total tests: 25
     Passed: 25
 Total time: 2.5 Seconds
```

---

## 7. ログの確認

### ログ出力先

- **コンソール**: 開発環境では標準出力にログが表示される。
- **ファイル**: `logs/` ディレクトリに日付ごとのログファイルが作成される（オプション）。

### ログレベルの変更

`appsettings.Development.json` でログレベルを調整:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "BlogDraftWebApp": "Debug",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### プレビューモードのログ

プレビューモードで表示されるプロンプト全文は、ログに記録されない（仕様）。

---

## 8. トラブルシューティング

### 問題: "LLM API key is not configured"

**原因**: User Secrets で API キーが設定されていない。

**解決策**:
```powershell
dotnet user-secrets set "OpenAI:ApiKey" "YOUR_API_KEY"
```

### 問題: "RAG 検索に失敗しました"

**原因**: Azure AI Search のエンドポイントまたは API キーが不正。

**解決策**:
1. Azure ポータルで検索サービスのエンドポイントと API キーを確認。
2. User Secrets で正しい値を設定:
   ```powershell
   dotnet user-secrets set "AzureAISearch:Endpoint" "https://YOUR_SERVICE.search.windows.net"
   dotnet user-secrets set "AzureAISearch:ApiKey" "YOUR_KEY"
   ```

### 問題: "文体カードが読み込めない"

**原因**: 文体カードのファイルパスが不正、または User Secrets に設定されていない。

**解決策**:
1. 文体カードファイルが存在することを確認。
2. User Secrets でパスを設定:
   ```powershell
   dotnet user-secrets set "StyleCard:FilePath" "C:\secrets\stylecard.md"
   ```
3. または、デフォルトの文体カードにフォールバック（ログで確認可能）。

### 問題: タイムアウトが発生する

**原因**: LLM モデルの推論が遅い、またはタイムアウト設定が短すぎる。

**解決策**:
```powershell
dotnet user-secrets set "OpenAI:RequestTimeoutSeconds" "180"
```

### 問題: 生成結果が短すぎる

**原因**: ユーザー入力が不足している、または LLM のパラメータが不適切。

**解決策**:
1. 概要をより詳細に記入（目的、手順、想定読者など）。
2. 最大トークン数を増やす:
   ```powershell
   dotnet user-secrets set "OpenAI:MaxTokens" "8192"
   ```

---

## 9. 本番環境へのデプロイ

### 9.1 Azure App Service へのデプロイ

#### 前提条件

- Azure CLI インストール済み
- Azure サブスクリプション

#### 手順

1. Azure App Service を作成:
   ```powershell
   az webapp create --resource-group myResourceGroup --plan myAppServicePlan --name blog-draft-webapp --runtime "DOTNET|10.0"
   ```

2. Application Settings で秘匿情報を設定:
   ```powershell
   az webapp config appsettings set --resource-group myResourceGroup --name blog-draft-webapp --settings OpenAI__ApiKey="YOUR_API_KEY" OpenAI__Model="gpt-4o"
   ```

3. Key Vault との統合（推奨）:
   ```powershell
   az keyvault create --resource-group myResourceGroup --name myKeyVault
   az keyvault secret set --vault-name myKeyVault --name OpenAIApiKey --value "YOUR_API_KEY"
   az webapp identity assign --resource-group myResourceGroup --name blog-draft-webapp
   az keyvault set-policy --name myKeyVault --object-id <IDENTITY_ID> --secret-permissions get
   ```

4. アプリをデプロイ:
   ```powershell
   dotnet publish -c Release -o ./publish
   az webapp deployment source config-zip --resource-group myResourceGroup --name blog-draft-webapp --src ./publish.zip
   ```

### 9.2 Docker コンテナでのデプロイ

#### Dockerfile 例

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/BlogDraftWebApp/BlogDraftWebApp.csproj", "BlogDraftWebApp/"]
RUN dotnet restore "BlogDraftWebApp/BlogDraftWebApp.csproj"
COPY src/ .
WORKDIR "/src/BlogDraftWebApp"
RUN dotnet build "BlogDraftWebApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "BlogDraftWebApp.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "BlogDraftWebApp.dll"]
```

#### ビルドと実行

```powershell
docker build -t blog-draft-webapp .
docker run -p 5001:443 -e OpenAI__ApiKey="YOUR_API_KEY" blog-draft-webapp
```

---

## 10. 参考リソース

- [仕様書](./spec.md): 機能要件と受け入れ条件
- [技術調査](./research.md): 技術選定の背景
- [データモデル](./data-model.md): エンティティとバリデーション
- [API 仕様](./contracts/openapi.yaml): OpenAPI 定義
- [実装計画](./plan.md): プロジェクト構造と進め方

---

## Next Steps

1. 実装を開始: `/speckit.tasks` コマンドで tasks.md を生成し、実装タスクに取り組む。
2. CI/CD 設定: GitHub Actions または Azure DevOps Pipeline を設定。
3. 品質改善: プレビューモードで生成品質を確認し、文体カードやプロンプトを調整。

---

**完了**: Phase 1（research.md、data-model.md、contracts/、quickstart.md）のすべてのドキュメントが作成されました。次のステップは `/speckit.tasks` コマンドで tasks.md を生成し、実装フェーズに移行します。
