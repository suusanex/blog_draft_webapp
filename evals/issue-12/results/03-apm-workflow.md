# 実生成本文

経路: workflow（アウトライン確定後の本文生成）
モデル: `gpt-5.6-sol`
ステータス: 成功（形式修復後に本文回収）

```markdown
# apmでカスタムエージェントを取得する

apmは、カスタムエージェントのパッケージマネージャとして使える。

https://github.com/microsoft/apm

Gitリポジトリ上のパスを列挙するだけで必要なカスタムエージェントを取得できるため、新しいリポジトリに取り組むたびに発生する反復作業が楽になる。

## Gitリポジトリ上のパスからインストール

`apm install`に複数のファイルパスを指定して取得できる。

```powershell
apm install `
  suusanex/coding_agent_plan_and_verify_process/.github/agents/plan-kernel.agent.md `
  suusanex/coding_agent_plan_and_verify_process/.github/agents/change-risk-triage.agent.md
```

この例では、同じGitリポジトリにある2つのカスタムエージェントを列挙している。

## 更新とターゲット変換

取得後の更新には`--update`を使う。Copilot向けカスタムエージェントからCodex向けにも変換する場合は`--target copilot,codex`、Skills向けには`--target agent-skills`を指定する。

Skillsの入力パス表記は`.agent/skills`としている。ただし、これらの表記やオプションの正確な構文・挙動について、技術的な確認はまだ実施していない。

## パッケージ定義から取得

公開側にapmのパッケージ定義があれば、個別ファイルを意識せず、パッケージとして取得できる。指定例は次のとおり。

```text
suusanex/coding_agent_plan_and_verify_process/apm-packages/token-aware-guardrail-kernel-flow#v0.1.0
```

# 厳密に使い込まなくても便利

apmは、全オプションを把握したり、配布側の詳細設計まで踏み込んだりしなくても便利に使える。まずは取得や更新、ターゲット変換によって、新しいリポジトリでの反復作業を減らす用途から活用するとよい。
```
