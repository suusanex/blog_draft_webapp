# Issue #12 実モデル評価結果

実施日: 2026-09-05
モデル: `gpt-5.6-sol`
対象: one-shot `/draft` と workflow（アウトライン生成・確定後の本文生成）
共通設定: 同じモデル、同じ設定、同じ外部文体カード。生成時の RAG 呼び出しは 0 件。
出力形式: JSON Schema Structured Output を要求し、形式不良時は本文と要確認事項を含むJSON全体の有限再整形を最大1回実施。workflow のアウトラインは壊れたJSONに限り再整形を最大2回実施。

API キー、文体カード本文、セッションの初期入力は記録しない。生成本文は Issue #12 の評価入力に対する出力であり、内容確認用に結果フォルダーへ保存する。

## ベースライン

ManualOnly。変更前後の同一モデル・設定・入力による実データ比較は手動受入作業と定義し、今回の実装では実施しない。`baseline/` は空のままであり、スタブやプロンプトテストを変更前結果の代替にはしない。

## 実行結果

| 入力 | one-shot `/draft` | workflow（アウトライン確定 → 本文） | keep/drop 判定 |
|---|---|---|---|
| `01-purpose-review.md` | 成功 | 成功 | 合格 |
| `02-wpf-slider.md` | 成功 | 成功 | 合格 |
| `03-apm.md` | 成功（形式修復後に本文回収） | 成功（形式修復後に本文回収） | 合格 |
| `04-wpa-file-io.md` | 成功 | 成功 | 合格 |

生成本文の全文は次の8ファイルに保存した。

- [01 one-shot](results/01-purpose-review-one-shot.md)、[01 workflow](results/01-purpose-review-workflow.md)
- [02 one-shot](results/02-wpf-slider-one-shot.md)、[02 workflow](results/02-wpf-slider-workflow.md)
- [03 one-shot](results/03-apm-one-shot.md)、[03 workflow](results/03-apm-workflow.md)
- [04 one-shot](results/04-wpa-file-io-one-shot.md)、[04 workflow](results/04-wpa-file-io-workflow.md)

## keep/drop による確認

- `01-purpose-review.md`: 別エージェントへゴールと PR だけを渡す狙い、仕様準拠と目的未達の区別、実績の留保、自己復旧へ膨張する例、準備 URL・実行例・最大3回が紹介対象の性質であること、準備負担の留保を両経路で保持した。入力にない製品仕様や定量的保証は追加せず、確認事項は `openQuestions` に分離した。
- `02-wpf-slider.md`: Slider と公式 URL、0〜1・0.1 の例、予想・マウス・キーボード・偶然の一致・`LargeChange`/`SmallChange` という気付きの流れを両経路で保持した。Slider 全体の入門説明は追加していない。
- `03-apm.md`: apm の紹介と公式 URL、パス列挙、install コマンド、`--update`、`--target copilot,codex`、`--target agent-skills`、`.agent/skills` の留保、パッケージ指定例、反復作業が楽になる結びを両経路で保持した。未確認のオプションは断定せず、workflow では `openQuestions` に分離した。
- `04-wpa-file-io.md`: グループ表示と時系列表示の対比、黄色い境界、左グループ／右1行1項目、列順、日時まで境界を動かす操作、Start 完全一致の条件、知らなかった人への提案を両経路で保持した。`EventType（必要なら SubType）` の表記は入力に明記された内容の範囲であり、追加説明とは判定していない。トレース取得手順や WPA 全体の解説は追加していない。

文字数、見出し数、項目数は合否に使っていない。形式修復は内容品質の合格判定とは分離し、本文と要確認事項を含む完全なJSONを受け取った後に keep/drop を確認する。なお、記録済みの8件はこの修正前の実モデル観測であり、修正後の実モデル再実行結果ではない。

## 未検証・人手で必要な作業

- 変更前と変更後の同一モデル比較は、手動受入作業（ManualOnly）として未実施。
- `apm` のコマンド、オプション、パス表記、パッケージ指定例の技術的な正しさは、この評価では検証せず、本文でも留保または `openQuestions` として扱った。
- E2E テスト8件は既存設定どおりスキップ。ブラウザーを使った人手の画面確認は未実施。
