param(
    [switch]$Open = $false
)

# Resolve repo root relative to this script's location
$RepoRoot = (Get-Item (Join-Path $PSScriptRoot ".." )).FullName
$SolutionPath = Join-Path $RepoRoot "NextWave.Jellyfish.Tests.sln"
$SettingsPath = Join-Path $RepoRoot "coverlet.runsettings"

if (-not (Test-Path $SolutionPath)) {
    Write-Error "Solution not found at: $SolutionPath"
    exit 1
}
if (-not (Test-Path $SettingsPath)) {
    Write-Error "RunSettings file not found at: $SettingsPath"
    exit 1
}

Write-Host "Running tests with coverage..." -ForegroundColor Cyan

dotnet test $SolutionPath --nologo --collect:"XPlat Code Coverage" --settings $SettingsPath --logger "trx;LogFileName=TestResults.trx"

# Find the latest Cobertura XML anywhere under the repo (covers multiple test projects)
$coverageFile = Get-ChildItem -Path $RepoRoot -Recurse -Filter "coverage.cobertura.xml" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($coverageFile) {
    Write-Host "Coverage report (Cobertura XML): $($coverageFile.FullName)" -ForegroundColor Green

    # Ensure local dotnet tools are available (for reportgenerator)
    dotnet tool restore | Out-Null

    $OutDir = Join-Path $RepoRoot "coverage"
    New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

    Write-Host "Generating HTML report to $OutDir..." -ForegroundColor Cyan
    dotnet tool run reportgenerator -reports:"$($coverageFile.FullName)" -targetdir:"$OutDir" -reporttypes:'Html;TextSummary'

    $indexHtml = Join-Path $OutDir "index.html"
    if (Test-Path $indexHtml) {
        Write-Host "HTML coverage report: $indexHtml" -ForegroundColor Green
        if ($Open) { Invoke-Item $indexHtml }
    } else {
        Write-Warning "HTML report not found in $OutDir"
    }

    $summary = Join-Path $OutDir "Summary.txt"
    if (Test-Path $summary) {
        Write-Host "Coverage Summary:" -ForegroundColor Yellow
        Get-Content $summary | Select-Object -First 12 | ForEach-Object { Write-Host $_ }
    }
} else {
    Write-Warning "Coverage file not found. Check the TestResults subfolders under the repo."
}

