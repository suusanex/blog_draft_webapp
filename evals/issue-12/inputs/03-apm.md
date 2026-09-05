apm をカスタムエージェントのパッケージマネージャとして紹介する: https://github.com/microsoft/apm

Git リポジトリ上のパスを列挙するだけで取得でき、新しいリポジトリに取り組む際の反復作業が楽になる。

apm install `
  suusanex/coding_agent_plan_and_verify_process/.github/agents/plan-kernel.agent.md `
  suusanex/coding_agent_plan_and_verify_process/.github/agents/change-risk-triage.agent.md

更新の `--update`、Copilot 向けカスタムエージェントから Codex 向けにも変換する `--target copilot,codex`、Skills 向けの `--target agent-skills` も説明対象。入力の Skills パス表記は `.agent/skills`。表記やオプションの技術的確認は未実施。

公開側に apm パッケージ定義があれば、個別ファイルを意識せずパッケージとして取得できる。例: `suusanex/coding_agent_plan_and_verify_process/apm-packages/token-aware-guardrail-kernel-flow#v0.1.0`

厳密に使い込まなくても便利なので活用しよう。全オプションや配布側の詳細設計へは広げない。
