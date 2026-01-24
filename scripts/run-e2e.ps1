# E2E 用: Kestrel 起動 → health 確認 → E2E 実行 → 終了
$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$baseUrl = "http://localhost:5055"
$env:ASPNETCORE_URLS = $baseUrl
$env:DOTNET_ENVIRONMENT = "Development"
$env:E2E_BASE_URL = $baseUrl
$env:E2E__StubLlm = "true"
$env:AzureAISearch__Enabled = "false"
$env:OpenAI__ApiKey = "test-key"
$env:OpenAI__Model = "test-model"
$env:OpenAI__MaxTokens = "256"
$env:StyleCard__Title = "E2E Style"
$env:StyleCard__SystemPrompt = "You are a test assistant."
$env:StyleCard__Content = "# Style\n- Test output"

Write-Host "Building solution..."
& dotnet build "BlogDraftWebApp.sln"

$playwrightScript = Join-Path $repoRoot "tests/BlogDraftWebApp.E2E.Tests/bin/Debug/net10.0/playwright.ps1"
if (Test-Path $playwrightScript)
{
    Write-Host "Installing Playwright browsers..."
    & $playwrightScript install
}

Write-Host "Starting web app..."
$logDir = Join-Path $repoRoot "artifacts"
if (-not (Test-Path $logDir))
{
    New-Item -ItemType Directory -Path $logDir | Out-Null
}
$stdoutLogPath = Join-Path $logDir "e2e-app.out.log"
$stderrLogPath = Join-Path $logDir "e2e-app.err.log"
if (Test-Path $stdoutLogPath)
{
    Remove-Item $stdoutLogPath -Force
}
if (Test-Path $stderrLogPath)
{
    Remove-Item $stderrLogPath -Force
}
$appProcess = Start-Process -FilePath "dotnet" -ArgumentList @("run", "--project", "src/BlogDraftWebApp/BlogDraftWebApp.csproj", "--no-build", "--no-launch-profile", "--urls", $baseUrl) -PassThru -RedirectStandardOutput $stdoutLogPath -RedirectStandardError $stderrLogPath

try
{
    Write-Host "Waiting for health endpoint..."
    $healthUrl = "$baseUrl/health"
    $ready = $false
    for ($i = 0; $i -lt 60; $i++)
    {
        try
        {
            if ($appProcess.HasExited)
            {
                throw "Web app exited unexpectedly. See logs: $stdoutLogPath, $stderrLogPath"
            }

            $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 5 -MaximumRedirection 0
            if ($response.StatusCode -eq 200)
            {
                $ready = $true
                break
            }
            if ($response.StatusCode -ge 300 -and $response.StatusCode -lt 400 -and $response.Headers.Location)
            {
                $redirectUri = [Uri]$response.Headers.Location
                if ($redirectUri.IsAbsoluteUri)
                {
                    $baseUrl = $redirectUri.GetLeftPart([System.UriPartial]::Authority)
                    $env:E2E_BASE_URL = $baseUrl
                    $healthUrl = "$baseUrl/health"
                }
            }
        }
        catch
        {
            Start-Sleep -Seconds 1
        }
    }

    if (-not $ready)
    {
        if (Test-Path $stdoutLogPath)
        {
            Write-Host "--- app stdout (tail) ---"
            Get-Content $stdoutLogPath -Tail 200
        }
        if (Test-Path $stderrLogPath)
        {
            Write-Host "--- app stderr (tail) ---"
            Get-Content $stderrLogPath -Tail 200
        }
        throw "Health check failed: $healthUrl"
    }

    Write-Host "Running E2E tests..."
    & dotnet test "tests/BlogDraftWebApp.E2E.Tests/BlogDraftWebApp.E2E.Tests.csproj"
}
finally
{
    if ($null -ne $appProcess -and -not $appProcess.HasExited)
    {
        Write-Host "Stopping web app..."
        Stop-Process -Id $appProcess.Id -Force
    }
}
