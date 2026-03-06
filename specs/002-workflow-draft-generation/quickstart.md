# Quickstart: ワークフロー型ブログ下書き生成

## 前提

- .NET 10 SDK (`dotnet --version` が `10.0.103`)
- `global.json` により SDK を固定
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

## 機能構成（現行）

- ワークフロー本体: **2段階**
  - Step1: アウトライン生成・編集・確定
  - Step2: 下書き生成・編集・確定（確定で `Completed`）
- タイトル/冒頭段落案: **独立機能**
  - UI: `/title-hook`
  - API: `/titlehook`, `/titlehook/preview`
  - WorkflowSession は引き継がない

## 主要画面

- `http://localhost:5000/workflow/outline`
- `http://localhost:5000/workflow/draft/{sessionId}`
- `http://localhost:5000/title-hook`
- `http://localhost:5000/workflow/admin`

## API確認

### 1. セッション作成

```powershell
curl -X POST http://localhost:5000/workflow/sessions -H "Content-Type: application/json" -d "{\"overview\":\"0123456789\"}"
```

### 2. アウトライン生成（Step1）

```powershell
curl -X POST http://localhost:5000/workflow/sessions/{sessionId}/steps/outline/generate -H "Content-Type: application/json" -d "{\"regenerate\":false}"
```

### 3. アウトライン確定（Step1）

```powershell
curl -X POST http://localhost:5000/workflow/sessions/{sessionId}/steps/outline/confirm -H "Content-Type: application/json" -d "{\"confirmedContent\":\"- 章1\n  - 論点A\n- 章2\"}"
```

### 4. 下書き生成（Step2）

```powershell
curl -X POST http://localhost:5000/workflow/sessions/{sessionId}/steps/draft/generate -H "Content-Type: application/json" -d "{\"regenerate\":false}"
```

### 5. 下書き確定（Completedへ遷移）

```powershell
curl -X POST http://localhost:5000/workflow/sessions/{sessionId}/steps/draft/confirm -H "Content-Type: application/json" -d "{\"confirmedContent\":\"十分な長さの下書き本文...\"}"
```

### 6. 独立機能: タイトル/冒頭段落案の生成

```powershell
curl -X POST http://localhost:5000/titlehook -H "Content-Type: application/json" -d "{\"articleBody\":\"完成した本文をここに入力...\"}"
```

### 7. 独立機能: プロンプトプレビュー

```powershell
curl -X POST http://localhost:5000/titlehook/preview -H "Content-Type: application/json" -d "{\"articleBody\":\"完成した本文をここに入力...\"}"
```

## テスト

```powershell
dotnet test
```
