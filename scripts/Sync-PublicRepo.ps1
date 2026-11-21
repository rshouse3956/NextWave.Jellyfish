<#
.SYNOPSIS
Copies this (private) repo's committed HEAD into the public repo, after checking that nothing identifying is in it.

.DESCRIPTION
Exports the files tracked at HEAD (git archive: no .env, untracked or ignored files), so the public repo's files match
the private repo's committed HEAD exactly. Before committing, every file's bytes are scanned against the regular
expressions in .publish-blocklist (one per line, # for comments). Office files (.docx and other zip files) are
unpacked and scanned too. Any match stops the run and leaves the public repo as it was.

.publish-blocklist is git-ignored on purpose: it holds the values it protects (coordinates, addresses, emails), so
it must never be published.

Modes:
  default  Sync: adds one commit to the public repo with the changes since the last sync (keeps its history).
  -Fresh   Rebuild: deletes the public folder and recreates it as a single commit. Only for a copy that hasn't been
           pushed; refused if the folder has a remote, unless -Force.

.EXAMPLE
./scripts/Sync-PublicRepo.ps1                 # sync, commit locally, don't push
./scripts/Sync-PublicRepo.ps1 -Push           # sync and push
./scripts/Sync-PublicRepo.ps1 -Fresh          # rebuild the unpublished one-commit copy
./scripts/Sync-PublicRepo.ps1 -ScanOnly       # just scan this repo's HEAD
#>
[CmdletBinding()]
param(
    [string]$Source = (Split-Path $PSScriptRoot -Parent),
    [string]$Target = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'NextWave.Jellyfish.Public'),
    [string]$Blocklist = (Join-Path (Split-Path $PSScriptRoot -Parent) '.publish-blocklist'),
    [string]$AuthorName = 'Ron Shouse',
    [string]$AuthorEmail = '193299930+rshouse3956@users.noreply.github.com',
    [string]$Message,
    [switch]$Fresh,
    [switch]$Force,
    [switch]$Push,
    [switch]$ScanOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.IO.Compression.FileSystem

# Invoke-Git <repo> <git args...>. A plain function (no param block) so git flags such as -o, -q and -r pass through
# in $args instead of being taken as PowerShell parameters.
function Invoke-Git {
    $Repo = $args[0]
    $GitArgs = @($args | Select-Object -Skip 1)
    $out = & git -C $Repo @GitArgs 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($GitArgs -join ' ') failed in ${Repo}:`n$out" }
    return $out
}

function Get-BlockPatterns {
    if (-not (Test-Path $Blocklist)) {
        throw "Blocklist not found: $Blocklist. Create it (one regex per line) before publishing; it is git-ignored."
    }
    $patterns = @(Get-Content $Blocklist | ForEach-Object { $_.Trim() } | Where-Object { $_ -and -not $_.StartsWith('#') })
    if ($patterns.Count -eq 0) { throw "Blocklist $Blocklist has no patterns." }
    foreach ($p in $patterns) { [void][regex]::new($p) }   # fail early on a bad regex
    return $patterns
}

# Scans the files tracked in $Repo (raw bytes, plus the contents of zip-based files such as .docx) and extra text.
function Find-Blocked {
    param([string]$Repo, [string[]]$Patterns, [string[]]$ExtraText = @())
    $regex = [regex]::new(($Patterns | ForEach-Object { "(?:$_)" }) -join '|', 'IgnoreCase')
    $hits = [Collections.Generic.List[string]]::new()
    $files = @(Invoke-Git $Repo ls-files -z | Out-String) -split "`0" | Where-Object { $_ }
    foreach ($rel in $files) {
        $path = Join-Path $Repo $rel
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $bytes = [IO.File]::ReadAllBytes($path)
        $texts = @{ $rel = [Text.Encoding]::Latin1.GetString($bytes) }
        if ($bytes.Length -gt 4 -and $bytes[0] -eq 0x50 -and $bytes[1] -eq 0x4B -and $bytes[2] -eq 3 -and $bytes[3] -eq 4) {
            $zip = [IO.Compression.ZipFile]::OpenRead($path)
            try {
                foreach ($e in $zip.Entries) {
                    if ($e.Length -eq 0) { continue }
                    $r = [IO.StreamReader]::new($e.Open())
                    try { $texts["$rel!$($e.FullName)"] = $r.ReadToEnd() } finally { $r.Dispose() }
                }
            } finally { $zip.Dispose() }
        }
        foreach ($k in $texts.Keys) {
            foreach ($m in $regex.Matches($texts[$k])) { $hits.Add("${k}: '$($m.Value)'") }
        }
    }
    foreach ($t in $ExtraText) { foreach ($m in $regex.Matches($t)) { $hits.Add("commit message: '$($m.Value)'") } }
    return @($hits | Sort-Object -Unique)
}

$Source = (Resolve-Path $Source).Path
$patterns = Get-BlockPatterns
$sourceHead = (Invoke-Git $Source rev-parse HEAD | Out-String).Trim()

if ($ScanOnly) {
    $hits = Find-Blocked -Repo $Source -Patterns $patterns
    if ($hits) { Write-Host "Blocked content at HEAD ($($hits.Count)):" -ForegroundColor Red; $hits | ForEach-Object { "  $_" }; exit 1 }
    Write-Host "HEAD $($sourceHead.Substring(0,7)) is clean ($($patterns.Count) patterns)." -ForegroundColor Green
    exit 0
}

if (Invoke-Git $Source status --porcelain --untracked-files=no) {
    Write-Warning "The private repo has uncommitted changes; only committed HEAD ($($sourceHead.Substring(0,7))) is copied."
}

# --- Prepare the target -------------------------------------------------------------------------------------------
$targetIsRepo = Test-Path (Join-Path $Target '.git')
if ($Fresh -and (Test-Path $Target)) {
    if ($targetIsRepo -and (Invoke-Git $Target remote) -and -not $Force) {
        throw "$Target has a remote, so it may be published. -Fresh would replace its history; use a sync, or -Force."
    }
    Remove-Item -LiteralPath $Target -Recurse -Force
    $targetIsRepo = $false
}
if (-not $targetIsRepo) {
    if ((Test-Path $Target) -and (Get-ChildItem -LiteralPath $Target -Force)) { throw "$Target exists, isn't a git repo and isn't empty." }
    New-Item -ItemType Directory -Force -Path $Target | Out-Null
    Invoke-Git $Target init -q -b main | Out-Null
    $autocrlf = (& git -C $Source config core.autocrlf)
    if ($autocrlf) { Invoke-Git $Target config core.autocrlf $autocrlf | Out-Null }
}
$Target = (Resolve-Path $Target).Path
Invoke-Git $Target config user.name $AuthorName | Out-Null
Invoke-Git $Target config user.email $AuthorEmail | Out-Null

$hasHead = $true
& git -C $Target rev-parse -q --verify HEAD *> $null; if ($LASTEXITCODE -ne 0) { $hasHead = $false }
if ($hasHead -and (Invoke-Git $Target status --porcelain --untracked-files=no)) {
    throw "$Target has uncommitted changes to tracked files; commit or discard them first."
}

# --- Copy HEAD's files --------------------------------------------------------------------------------------------
$tar = Join-Path ([IO.Path]::GetTempPath()) "jellyfish-public-$([guid]::NewGuid().ToString('N')).tar"
try {
    Invoke-Git $Source archive --format=tar -o $tar HEAD | Out-Null
    if ($hasHead) { Invoke-Git $Target rm -r -q --ignore-unmatch . | Out-Null }   # tracked files only
    & tar -xf $tar -C $Target
    if ($LASTEXITCODE -ne 0) { throw "tar failed extracting $tar" }
} finally { Remove-Item -LiteralPath $tar -Force -ErrorAction SilentlyContinue }
Invoke-Git $Target add -A | Out-Null

& git -C $Target diff --cached --quiet
if ($hasHead -and $LASTEXITCODE -eq 0) {
    Write-Host "Already up to date with $($sourceHead.Substring(0,7)); nothing to commit." -ForegroundColor Green
    exit 0
}

# --- Commit message -----------------------------------------------------------------------------------------------
if (-not $Message) {
    if (-not $hasHead) {
        $Message = "NextWave.Jellyfish: schedule and pattern manager for Jellyfish lighting controllers.`n`n" +
            "A .NET 10 API and Vue client that syncs folders, patterns and zones from a`n" +
            "Jellyfish controller, runs schedules (fixed times, sunrise/sunset, holidays),`n" +
            "and creates patterns with an optional local AI assistant (Ollama)."
    } else {
        $last = ((Invoke-Git $Target log -1 --format=%B | Out-String) -split "`n" |
            Where-Object { $_ -match '^Synced-From: ([0-9a-f]{40})' } | ForEach-Object { $Matches[1] } | Select-Object -First 1)
        $subjects = if ($last) { @(Invoke-Git $Source log --reverse --format='- %s' "$last..HEAD") } else { @() }
        $Message = "Sync with the private repo`n" + $(if ($subjects) { "`n" + ($subjects -join "`n") } else { '' })
    }
}
$Message = "$($Message.TrimEnd())`n`nSynced-From: $sourceHead"

# --- Scan, then commit --------------------------------------------------------------------------------------------
$hits = Find-Blocked -Repo $Target -Patterns $patterns -ExtraText @($Message)
if ($hits) {
    if ($hasHead) { Invoke-Git $Target reset -q --hard HEAD | Out-Null }
    Write-Host "Stopped: blocked content found ($($hits.Count)); nothing was committed." -ForegroundColor Red
    $hits | ForEach-Object { "  $_" }
    exit 1
}

$msgFile = Join-Path ([IO.Path]::GetTempPath()) "jellyfish-public-msg-$([guid]::NewGuid().ToString('N')).txt"
try {
    [IO.File]::WriteAllText($msgFile, $Message, [Text.UTF8Encoding]::new($false))
    Invoke-Git $Target commit -q -F $msgFile | Out-Null
} finally { Remove-Item -LiteralPath $msgFile -Force -ErrorAction SilentlyContinue }

$srcTree = (Invoke-Git $Source rev-parse 'HEAD^{tree}' | Out-String).Trim()
$dstTree = (Invoke-Git $Target rev-parse 'HEAD^{tree}' | Out-String).Trim()
if ($srcTree -ne $dstTree) { throw "Tree mismatch after commit: private $srcTree, public $dstTree." }

$commit = (Invoke-Git $Target log -1 --format='%h %s' | Out-String).Trim()
Write-Host "Committed in ${Target}: $commit (matches private $($sourceHead.Substring(0,7)); scan clean)." -ForegroundColor Green

if ($Push) {
    if (-not (Invoke-Git $Target remote)) { throw "No remote in $Target; add one with git remote add origin <url>." }
    Invoke-Git $Target push -u origin main | Out-Null
    Write-Host 'Pushed.' -ForegroundColor Green
} else {
    Write-Host "Not pushed. Review, then: git -C `"$Target`" push" -ForegroundColor Yellow
}
