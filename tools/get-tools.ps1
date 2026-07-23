# Downloads the external tools (ffmpeg, piper, rhubarb) into tools/bin/,
# verifying each archive against the SHA256 pinned in tools.lock.json.
# Used by developers and by the release workflow. Never commits binaries.
#
# First-time pinning: run with -UpdateLock to download, compute, and write
# the hashes into tools.lock.json. Normal runs refuse unpinned entries.

[CmdletBinding()]
param(
    [switch]$UpdateLock
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$root = Split-Path -Parent $PSCommandPath
$lockPath = Join-Path $root "tools.lock.json"
$binRoot = Join-Path $root "bin"
$cacheRoot = Join-Path $root "cache"

function Get-FileSha256([string]$Path) {
    (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$lock = Get-Content $lockPath -Raw | ConvertFrom-Json
$lockChanged = $false

foreach ($tool in $lock.tools) {
    $name = $tool.name
    $dest = Join-Path $binRoot $name
    $marker = Join-Path $dest ".hash"

    if ((Test-Path $marker) -and $tool.sha256 -and ((Get-Content $marker -Raw).Trim() -eq $tool.sha256)) {
        Write-Host "[$name] up to date"
        continue
    }

    New-Item -ItemType Directory -Force $cacheRoot | Out-Null
    $zipPath = Join-Path $cacheRoot "$name.zip"

    $needDownload = $true
    if ((Test-Path $zipPath) -and $tool.sha256) {
        if ((Get-FileSha256 $zipPath) -eq $tool.sha256) { $needDownload = $false }
    }
    if ($needDownload) {
        Write-Host "[$name] downloading $($tool.url)"
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $tool.url -OutFile $zipPath -UseBasicParsing
    }

    $actual = Get-FileSha256 $zipPath
    if (-not $tool.sha256) {
        if (-not $UpdateLock) {
            throw "[$name] tools.lock.json has no sha256 for this tool. Run get-tools.ps1 -UpdateLock once to pin it."
        }
        $tool.sha256 = $actual
        $lockChanged = $true
        Write-Host "[$name] pinned sha256 $actual"
    }
    elseif ($actual -ne $tool.sha256) {
        throw "[$name] SHA256 mismatch. Expected $($tool.sha256), got $actual. Refusing to install."
    }

    $extractDir = Join-Path $cacheRoot "$name-extract"
    if (Test-Path $extractDir) { Remove-Item -Recurse -Force $extractDir }
    Expand-Archive -Path $zipPath -DestinationPath $extractDir

    if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
    New-Item -ItemType Directory -Force $dest | Out-Null

    foreach ($rule in $tool.copy) {
        $fromRel = $rule.from -replace "/", "\"
        $from = Join-Path $extractDir $fromRel
        if (-not (Test-Path $from)) {
            throw "[$name] expected '$($rule.from)' inside the archive; not found."
        }
        if ($rule.from.EndsWith("/")) {
            $target = $dest
            if ($rule.to) {
                $target = Join-Path $dest $rule.to
                New-Item -ItemType Directory -Force $target | Out-Null
            }
            Copy-Item -Path (Join-Path $from "*") -Destination $target -Recurse -Force
        }
        else {
            $target = Join-Path $dest $rule.to
            $parent = Split-Path -Parent $target
            if ($parent) { New-Item -ItemType Directory -Force $parent | Out-Null }
            Copy-Item -Path $from -Destination $target -Force
        }
    }

    Remove-Item -Recurse -Force $extractDir
    Set-Content -Path $marker -Value $tool.sha256 -Encoding ascii
    Write-Host "[$name] installed to $dest"
}

if ($lockChanged) {
    $lock | ConvertTo-Json -Depth 8 | Set-Content -Path $lockPath -Encoding utf8
    Write-Host "tools.lock.json updated with pinned hashes."
}
Write-Host "Done."
