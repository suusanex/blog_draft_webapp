[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$packageRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$skillPath = Join-Path $packageRoot '.apm/skills/persistent-purpose-review/SKILL.md'

foreach ($path in @(
    (Join-Path $packageRoot 'apm.yml'),
    (Join-Path $packageRoot 'README.md'),
    $skillPath,
    (Join-Path $packageRoot 'scripts/test-apm-package-install.ps1')
)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing package file: $path" }
}

$manifest = Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'apm.yml')
$skill = Get-Content -Raw -LiteralPath $skillPath
$readme = Get-Content -Raw -LiteralPath (Join-Path $packageRoot 'README.md')

if ($manifest -notmatch '(?m)^name:\s*persistent-purpose-review\s*$' -or $manifest -notmatch '(?m)^version:\s*0\.3\.0\s*$') {
    throw 'APM package identity is invalid.'
}
if ($manifest -notmatch '(?ms)^targets:\s*$.*^\s+- copilot\s*$.*^\s+- codex\s*$.*^\s+- agent-skills\s*$') {
    throw 'APM package targets are invalid.'
}
if ($skill -notmatch 'purpose-review-runner version' -or $skill -notmatch 'protocolVersion.*`2`' -or $skill -notmatch 'runnerVersion' -or $skill -notmatch '0\.2\.2') {
    throw 'Skill does not fail closed on the Runner protocol boundary.'
}
if ($skill -notmatch 'status --run' -or $skill -notmatch 'RUNNING' -or $skill -notmatch 'jobStatus') {
    throw 'Skill does not poll durable job status.'
}
if ($skill -notmatch 'status.*やり直す') {
    throw 'Skill does not keep status polling idempotent after a failed CLI invocation.'
}
if ($skill -match '(?m)work --run') {
    throw 'Skill must not invoke the internal worker command.'
}
if ($skill -notmatch '公開protocol fields' -or $skill -notmatch '`findings`.*`message`.*`error`') {
    throw 'Skill does not use the complete public Runner result contract.'
}
if ($skill -notmatch 'ユーザーが明示したsourceを最優先' -or $skill -notmatch '複数文書が同じ目的を補完') {
    throw 'Skill context selection precedence is incomplete.'
}
if ($skill -notmatch '同じreviewer session' -or $skill -notmatch 'automatic round 4' -or $skill -notmatch '別providerへの切替を行わない') {
    throw 'Skill lifecycle stop contract is incomplete.'
}
if ($skill -match 'codex exec|grok --|copilot -|--session-id|--resume|--sandbox|--model|reasoning-effort') {
    throw 'Skill duplicates provider or session protocol details owned by the Runner.'
}
if ($readme -notmatch 'OS userごとに一度' -or $readme -notmatch 'work repositoryごと' -or $readme -notmatch '\$pr-review-remediation') {
    throw 'Package README does not preserve the three installation and ownership boundaries.'
}
if ($readme -match 'sandbox' -or $skill -match 'sandbox') {
    throw 'Package documents must not treat filesystem sandbox enforcement as a protocol invariant.'
}
if ($readme -notmatch 'non-modifying reviewer' -or $skill -notmatch 'repositoryを変更しない') {
    throw 'Package documents do not preserve the non-modifying reviewer contract.'
}
if ($readme -notmatch '0\.2\.2' -or $readme -notmatch 'apm update') {
    throw 'Package README does not require Runner 0.2.2 or later after APM-only updates.'
}

Write-Output 'Persistent Purpose Review package validation: PASS'
