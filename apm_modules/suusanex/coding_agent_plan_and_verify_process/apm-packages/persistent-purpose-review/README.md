# Persistent Purpose Review

`$persistent-purpose-review`は、元のimplementation parentが独立purpose reviewerのfindingを自分で修正し、同じreviewer sessionへ再reviewを依頼するAPM Skillです。session lifecycleは別配布の`purpose-review-runner`が決定的に管理します。

## Ownership boundary

| Component | Installation scope | Responsibility |
| --- | --- | --- |
| `purpose-review-runner` | 開発PCのOS userごとに一度 | provider CLI起動、same-session、non-modifying reviewer、最大3round、state、machine-readable result |
| `$persistent-purpose-review` | 利用するwork repositoryごと | purpose context選択、parent-owned remediation、terminal reporting |
| `$pr-review-remediation` | baseline PR reviewが必要なrepositoryごと | Goal Contextを使わないPR review plan作成 |

SkillはRunner binaryを内包、複製、自動download、installしません。このSkillは`purpose-review-runner` 0.2.2以上とprotocol v2を要求する。0.2.1はWindowsのrestrictive Job Object配下からworkerを独立起動できるが、Copilot CLIへreview payloadのMarkdownをattachmentとして渡すため、現在のCLIでは開始できない。0.2.0はdurable jobを導入したが、Windowsのrestrictive Job Object配下ではworkerを独立起動できない。0.1.xは`start`/`continue`が最終review JSONを同期的に返すため、coding agentのforeground command寿命と両立しない。Runner未導入、0.2.2未満、またはprotocol非互換ならfail closedで停止します。

## Install

先に[Purpose Review Runner](../../apps/PurposeReviewRunner/README.md)のversioned GitHub ReleaseをOS user単位で導入し、configを作成します。その後、対象repository rootでSkillを導入します。

```powershell
apm install suusanex/coding_agent_plan_and_verify_process/apm-packages/persistent-purpose-review --target copilot,codex,agent-skills
purpose-review-runner version
```

## Use

通常の実装指示へ次を加えます。

```text
実装完了後は $persistent-purpose-review に従ってpurpose reviewを完了してください。
```

Goal Contextやaccepted decision documentが会話で明示済みなら、そのpathも指定できます。補完関係にある複数文書はすべてcontextとして渡せます。現在のsourceが競合する場合だけ、parentがreview開始前に質問します。

`start`と`continue`は短時間でjobを登録するだけです。結果は同じ`runId`の`status`をpollingして取得します。`FINDINGS`では元のparentだけが修正とvalidationを行い、同じ`runId`で`continue`します。`COMPLETE`、`HUMAN_DECISION_REQUIRED`、`BLOCKED`、`ERROR`で終了します。polling中のCLI失敗では`status`だけをやり直します。

## Update and remove

```powershell
apm update
apm uninstall persistent-purpose-review
```

APM packageの更新・削除はRunner binary、user-level config、既存run stateを変更しません。`apm update`だけではCopilot CLIの標準入力prompt転送へ移行できない。Runner 0.2.2以上への更新はGitHub Release側で別に行う。

## Validation

```powershell
pwsh -NoProfile -File apm-packages/persistent-purpose-review/scripts/validate-persistent-purpose-review.ps1
pwsh -NoProfile -File apm-packages/persistent-purpose-review/scripts/test-apm-package-install.ps1
```

## Agent Plugin artifact

process semanticsの正本はこのpackageの`.apm/**`です。Agent Plugin artifactはpackage rootへchecked-inせず、repository共通builderでtemporary stageへ生成します。

```powershell
pwsh -NoProfile -File scripts/agent-plugins/build-agent-plugin.ps1 -Package persistent-purpose-review
pwsh -NoProfile -File scripts/agent-plugins/validate-agent-plugin-package.ps1 -Package persistent-purpose-review
```

APMがsupported distributionです。direct deploymentのstatusとevidenceは`tests/agent-plugin/qualification.json`に記録し、未観測のbehaviorをPASSへ昇格させません。
