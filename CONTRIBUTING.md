# コントリビューションガイド

## 前提

- .NET 10 SDK

## ビルド

```powershell
dotnet restore
dotnet build -c Release
```

## テスト

```powershell
dotnet test -c Release
```

## ローカル実行

```powershell
cd src/BlogDraftWebApp
dotnet run
```

## 重要なルール

- 秘匿情報（API キー、文体カード、エンドポイント URL 等）をコミットしない
- ログに秘匿情報（入力、プロンプト、文体カード等）を出力しない
- UnitTest / IntegrationTest は実 OS 環境を変更しない（レジストリ/サービス/ドライバ等）
- ソースコード上のコメントは日本語、ログ出力や例外メッセージは英語

## PR の作法

- 変更理由と影響範囲を日本語で記載
- 可能な限りテストを追加し、`dotnet test -c Release` が通ること
