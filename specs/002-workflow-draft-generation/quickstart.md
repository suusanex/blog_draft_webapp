# Quickstart: ワークフロー型ブログ下書き生成

## 前提

- .NET 10 SDK
- `src/BlogDraftWebApp/appsettings.json` の必須設定が投入済み
  - `OpenAI:ApiKey`
  - `OpenAI:Model`
  - `StyleCard:SystemPrompt`
  - `StyleCard:Content`

## 起動

```powershell
cd src/BlogDraftWebApp
dotnet run
```

## 主要画面

- `http://localhost:5000/workflow/outline`
- `http://localhost:5000/workflow/admin`

## API確認

### 1. セッション作成

```powershell
curl -X POST http://localhost:5000/workflow/sessions -H "Content-Type: application/json" -d "{\"overview\":\"0123456789\"}"
```

### 2. アウトライン生成

```powershell
curl -X POST http://localhost:5000/workflow/sessions/{sessionId}/steps/outline/generate -H "Content-Type: application/json" -d "{\"regenerate\":false}"
```

### 3. 下書き生成（アウトライン確定後）

```powershell
curl -X POST http://localhost:5000/workflow/sessions/{sessionId}/steps/draft/generate -H "Content-Type: application/json" -d "{\"regenerate\":false}"
```

### 4. タイトル生成（下書き確定後）

```powershell
curl -X POST http://localhost:5000/workflow/sessions/{sessionId}/steps/titlehook/generate -H "Content-Type: application/json" -d "{\"regenerate\":false}"
```

## テスト

```powershell
dotnet test
```
