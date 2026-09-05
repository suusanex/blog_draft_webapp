# 実生成本文

経路: one-shot `/draft`
モデル: `gpt-5.6-sol`
ステータス: 成功（形式修復後に本文回収）

```markdown
# apm でカスタムエージェントの導入を楽にする

`apm` は、カスタムエージェントのパッケージマネージャとして使える。

https://github.com/microsoft/apm

Git リポジトリ上のパスを列挙するだけで取得できるため、新しいリポジトリに取り組むたびに発生する反復作業を減らせる。

```powershell
apm install `
  suusanex/coding_agent_plan_and_verify_process/.github/agents/plan-kernel.agent.md `
  suusanex/coding_agent_plan_and_verify_process/.github/agents/change-risk-triage.agent.md
```

公開側に apm のパッケージ定義があれば、個別ファイルを意識せず、パッケージとして取得することもできる。

```text
suusanex/coding_agent_plan_and_verify_process/apm-packages/token-aware-guardrail-kernel-flow#v0.1.0
```

更新には `--update` を使う。Copilot 向けのカスタムエージェントを Codex 向けにも変換する場合は `--target copilot,codex`、Skills 向けには `--target agent-skills` を指定する。入力側の Skills パス表記は `.agent/skills` となる。

ただし、これらの表記やオプションについては、現時点では技術的な確認を実施していない。

全オプションや配布側の詳細設計まで把握していなくても、カスタムエージェントを繰り返し導入する用途には便利なので活用していきたい。
```
