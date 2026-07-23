# Builds a portable SadTalker presenter bundle (cpu or cuda variant):
# embeddable Python + pinned wheels + SadTalker source + weights + ffmpeg,
# smoke-tested (including a diacritic path), zipped into release parts, with
# a manifest ready for src/RoyalNewsDesk.Core/Resources/presenters.json.
#
# Usage: .\tools\build-presenter-bundle.ps1 -Variant cpu [-SkipSmokeTest] [-Upload]

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet("cpu", "cuda")][string]$Variant,
    [switch]$SkipSmokeTest,
    [switch]$Upload
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$repo = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
$cache = Join-Path $repo "tools\build\cache"
$work = Join-Path $repo "tools\build\$Variant\work"
$outDir = Join-Path $repo "tools\build\$Variant\dist"
$bundleSrc = Join-Path $repo "tools\presenter-bundle"
$engineId = "sadtalker-$Variant"
$sadTalkerCommit = "cd4c0465ae0b54a6f85af57f5c65fec9fe23e7f8"
$releaseTag = "presenter-engines-v1"

$pythonZipUrl = "https://www.python.org/ftp/python/3.10.11/python-3.10.11-embed-amd64.zip"
$getPipUrl = "https://bootstrap.pypa.io/get-pip.py"

# name, subdir (relative to engine\), url, expected bytes
$weights = @(
    @("SadTalker_V0.0.2_256.safetensors", "checkpoints", "https://github.com/OpenTalker/SadTalker/releases/download/v0.0.2-rc/SadTalker_V0.0.2_256.safetensors", 725066984),
    @("mapping_00109-model.pth.tar", "checkpoints", "https://github.com/OpenTalker/SadTalker/releases/download/v0.0.2-rc/mapping_00109-model.pth.tar", 155779231),
    @("mapping_00229-model.pth.tar", "checkpoints", "https://github.com/OpenTalker/SadTalker/releases/download/v0.0.2-rc/mapping_00229-model.pth.tar", 155521183),
    @("detection_Resnet50_Final.pth", "gfpgan\weights", "https://github.com/xinntao/facexlib/releases/download/v0.1.0/detection_Resnet50_Final.pth", 109497761),
    @("alignment_WFLW_4HG.pth", "gfpgan\weights", "https://github.com/xinntao/facexlib/releases/download/v0.1.0/alignment_WFLW_4HG.pth", 193670248)
)
if ($Variant -eq "cuda") {
    $weights += @(
        @("SadTalker_V0.0.2_512.safetensors", "checkpoints", "https://github.com/OpenTalker/SadTalker/releases/download/v0.0.2-rc/SadTalker_V0.0.2_512.safetensors", 725066984),
        @("GFPGANv1.4.pth", "gfpgan\weights", "https://github.com/TencentARC/GFPGAN/releases/download/v1.3.0/GFPGANv1.4.pth", 348632874),
        @("parsing_parsenet.pth", "gfpgan\weights", "https://github.com/xinntao/facexlib/releases/download/v0.2.2/parsing_parsenet.pth", 85331193)
    )
}

function Get-Cached([string]$Url, [string]$Name) {
    New-Item -ItemType Directory -Force $cache | Out-Null
    $path = Join-Path $cache $Name
    if (-not (Test-Path $path)) {
        Write-Host "[download] $Name"
        Invoke-WebRequest -Uri $Url -OutFile $path -UseBasicParsing
    }
    return $path
}

function Invoke-Python {
    param([string[]]$PyArgs)
    & (Join-Path $work "python\python.exe") @PyArgs
    if ($LASTEXITCODE -ne 0) { throw "python step failed: $($PyArgs -join ' ')" }
}

Write-Host "=== Building $engineId ==="
if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory -Force $work, $outDir | Out-Null

# --- 1. Python embeddable + pip -------------------------------------------
$pyZip = Get-Cached $pythonZipUrl "python-3.10.11-embed-amd64.zip"
Expand-Archive $pyZip (Join-Path $work "python")
# ..\engine puts SadTalker's own modules (src.*) on sys.path: embeddable
# Python ignores the working directory, so the layout bakes it in instead.
Set-Content -Path (Join-Path $work "python\python310._pth") -Encoding ascii -Value @"
python310.zip
.
Lib\site-packages
..\engine
import site
"@
$getPip = Get-Cached $getPipUrl "get-pip.py"
Invoke-Python @($getPip, "pip==24.0", "setuptools==69.5.1", "wheel==0.43.0", "--no-warn-script-location")

# --- 2. Torch + deps ------------------------------------------------------
if ($Variant -eq "cpu") { $torchIndex = "https://download.pytorch.org/whl/cpu" } else { $torchIndex = "https://download.pytorch.org/whl/cu121" }
Invoke-Python @("-m", "pip", "install", "torch==2.1.2", "torchvision==0.16.2", "--index-url", $torchIndex, "--no-warn-script-location")
Invoke-Python @("-m", "pip", "install", "-r", (Join-Path $bundleSrc "requirements-common.txt"), "--no-warn-script-location")

# Some pure-python sdists have setup.py files that import their own package,
# which can never work under embeddable Python (the ._pth removes the current
# directory from sys.path). Vendor those straight into site-packages.
function Install-PurePackage {
    param([string]$Name, [string]$Version, [string]$PackageDir, [string]$VersionFileBody)
    $meta = Invoke-RestMethod "https://pypi.org/pypi/$Name/$Version/json"
    $sdist = $meta.urls | Where-Object { $_.packagetype -eq "sdist" } | Select-Object -First 1
    $archive = Get-Cached $sdist.url $sdist.filename
    $extract = Join-Path $cache "$Name-$Version-extract"
    if (Test-Path $extract) { Remove-Item -Recurse -Force $extract }
    if ($sdist.filename.EndsWith(".zip")) {
        Expand-Archive $archive $extract
    }
    else {
        New-Item -ItemType Directory -Force $extract | Out-Null
        tar -xzf $archive -C $extract
        if ($LASTEXITCODE -ne 0) { throw "tar failed for $Name" }
    }
    $pkg = Get-ChildItem $extract -Recurse -Directory | Where-Object { $_.Name -eq $PackageDir -and (Test-Path (Join-Path $_.FullName "__init__.py")) } | Select-Object -First 1
    if ($null -eq $pkg) { throw "package dir $PackageDir not found in $Name sdist" }
    $target = Join-Path $work "python\Lib\site-packages\$PackageDir"
    if (Test-Path $target) { Remove-Item -Recurse -Force $target }
    Copy-Item $pkg.FullName $target -Recurse
    if ($VersionFileBody) {
        Set-Content -Path (Join-Path $target "version.py") -Value $VersionFileBody -Encoding ascii
    }
    Write-Host "[vendored] $Name $Version"
}

Install-PurePackage "filterpy" "1.4.5" "filterpy" $null
Install-PurePackage "face_alignment" "1.3.5" "face_alignment" $null
Invoke-Python @("-m", "pip", "install", "facexlib==0.3.0", "--no-deps", "--no-warn-script-location")
Install-PurePackage "basicsr" "1.4.2" "basicsr" "__version__ = '1.4.2'`n__gitsha__ = 'unknown'`nversion_info = (1, 4, 2)`n"
Install-PurePackage "gfpgan" "1.3.8" "gfpgan" "__version__ = '1.3.8'`n__gitsha__ = 'unknown'`nversion_info = (1, 3, 8)`n"

# pip check: only tb-nightly/gradio complaints are acceptable ("requires"
# lines can also name packages we intentionally satisfied out of band).
$pipCheck = & (Join-Path $work "python\python.exe") -m pip check 2>&1
$bad = $pipCheck | Where-Object { $_ -and $_ -notmatch "tb-nightly" -and $_ -notmatch "gradio" }
if ($bad) { Write-Warning "pip check: $($bad -join '; ')" }

# --- 3. SadTalker source at the pinned commit -----------------------------
$srcCache = Join-Path $cache "SadTalker-src"
if (-not (Test-Path $srcCache)) {
    git clone --quiet https://github.com/OpenTalker/SadTalker.git $srcCache
}
Push-Location $srcCache
git fetch --quiet origin
git checkout --quiet $sadTalkerCommit
Pop-Location
$engine = Join-Path $work "engine"
New-Item -ItemType Directory -Force $engine | Out-Null
robocopy $srcCache $engine /E /XD ".git" ".github" "docs" "examples" /XF "webui.bat" "webui.sh" "app_sadtalker.py" "launcher.py" /NFL /NDL /NJH /NJS | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with $LASTEXITCODE" }
$global:LASTEXITCODE = 0

# Keep one sample portrait and one short wav for smoke tests.
New-Item -ItemType Directory -Force (Join-Path $engine "examples\source_image"), (Join-Path $engine "examples\driven_audio") | Out-Null
$sampleImage = Get-ChildItem (Join-Path $srcCache "examples\source_image") -Filter "*.png" | Select-Object -First 1
$sampleAudio = Get-ChildItem (Join-Path $srcCache "examples\driven_audio") -Filter "*.wav" | Sort-Object Length | Select-Object -First 1
Copy-Item $sampleImage.FullName (Join-Path $engine "examples\source_image\sample.png")
Copy-Item $sampleAudio.FullName (Join-Path $engine "examples\driven_audio\sample.wav")
Copy-Item (Join-Path $bundleSrc "probe.py") (Join-Path $engine "probe.py")

# --- 4. Weights -----------------------------------------------------------
foreach ($w in $weights) {
    $name = $w[0]; $sub = $w[1]; $url = $w[2]; $expected = $w[3]
    $cached = Get-Cached $url $name
    $size = (Get-Item $cached).Length
    if ($size -ne $expected) { throw "$name size $size does not match expected $expected" }
    $dest = Join-Path $engine $sub
    New-Item -ItemType Directory -Force $dest | Out-Null
    Copy-Item $cached (Join-Path $dest $name)
}

# --- 5. ffmpeg + VC runtime ----------------------------------------------
New-Item -ItemType Directory -Force (Join-Path $work "bin") | Out-Null
Copy-Item (Join-Path $repo "tools\bin\ffmpeg\ffmpeg.exe") (Join-Path $work "bin\ffmpeg.exe")
foreach ($dll in @("msvcp140.dll", "vcruntime140.dll", "vcruntime140_1.dll", "vcomp140.dll")) {
    Copy-Item (Join-Path $env:SystemRoot "System32\$dll") (Join-Path $work "python\$dll")
}
New-Item -ItemType Directory -Force (Join-Path $work "jobs"), (Join-Path $work "cache"), (Join-Path $work "tmp") | Out-Null

# --- 6. Version + notices -------------------------------------------------
@{ engineId = $engineId; version = "1.0.0"; sourceCommit = $sadTalkerCommit; built = (Get-Date).ToString("yyyy-MM-dd") } |
    ConvertTo-Json | Set-Content (Join-Path $work "VERSION.json") -Encoding utf8
Copy-Item (Join-Path $bundleSrc "THIRD-PARTY-NOTICES.txt") (Join-Path $work "THIRD-PARTY-NOTICES.txt")

# --- 7. Smoke test (includes the diacritic-path proof) --------------------
if (-not $SkipSmokeTest) {
    Write-Host "=== Smoke test (diacritic path) ==="
    $smokeRoot = Join-Path $repo "tools\build\$Variant\smoke $([char]0x00E9)$([char]0x00EB) test"
    if (Test-Path $smokeRoot) { Remove-Item -Recurse -Force $smokeRoot }
    New-Item -ItemType Directory -Force $smokeRoot | Out-Null
    robocopy $work $smokeRoot /E /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "smoke copy failed" }
    $global:LASTEXITCODE = 0

    $jobDir = Join-Path $smokeRoot "jobs\smoke01"
    New-Item -ItemType Directory -Force (Join-Path $jobDir "out") | Out-Null
    Copy-Item (Join-Path $smokeRoot "engine\examples\source_image\sample.png") (Join-Path $jobDir "source.png")
    Copy-Item (Join-Path $smokeRoot "engine\examples\driven_audio\sample.wav") (Join-Path $jobDir "audio.wav")

    $env:PATH = "$smokeRoot\bin;$smokeRoot\python;$env:SystemRoot\System32;$env:SystemRoot"
    $env:PYTHONNOUSERSITE = "1"; $env:PYTHONUTF8 = "1"
    $env:TEMP = "$smokeRoot\tmp"; $env:TMP = "$smokeRoot\tmp"
    $env:TORCH_HOME = "$smokeRoot\cache\torch"; $env:NUMBA_CACHE_DIR = "$smokeRoot\cache\numba"
    if ($Variant -eq "cpu") { $env:CUDA_VISIBLE_DEVICES = "-1" } else { $env:CUDA_VISIBLE_DEVICES = "0" }

    Push-Location (Join-Path $smokeRoot "engine")
    $smokeArgs = @("-u", "inference.py",
        "--driven_audio", "../jobs/smoke01/audio.wav",
        "--source_image", "../jobs/smoke01/source.png",
        "--result_dir", "../jobs/smoke01/out",
        "--checkpoint_dir", "./checkpoints",
        "--preprocess", "full", "--still", "--pose_style", "0", "--expression_scale", "1.0")
    if ($Variant -eq "cpu") { $smokeArgs += @("--cpu", "--size", "256", "--batch_size", "1") }
    else { $smokeArgs += @("--size", "512", "--enhancer", "gfpgan", "--batch_size", "2") }
    & (Join-Path $smokeRoot "python\python.exe") @smokeArgs
    $smokeExit = $LASTEXITCODE
    Pop-Location
    if ($smokeExit -ne 0) { throw "smoke render failed with exit $smokeExit" }
    $produced = Get-ChildItem (Join-Path $jobDir "out") -Filter "*.mp4"
    if (-not $produced) { throw "smoke render produced no mp4" }
    Write-Host "Smoke render OK: $($produced[0].Name) ($([math]::Round($produced[0].Length/1MB,1)) MB)"

    & (Join-Path $smokeRoot "python\python.exe") -u (Join-Path $smokeRoot "engine\probe.py")
    if ($LASTEXITCODE -ne 0) { throw "probe failed in smoke copy" }
    Remove-Item -Recurse -Force $smokeRoot
}

# --- 8. Prune -------------------------------------------------------------
Write-Host "=== Pruning ==="
Get-ChildItem $work -Recurse -Directory -Filter "__pycache__" | Remove-Item -Recurse -Force
$sitePackages = Join-Path $work "python\Lib\site-packages"
foreach ($d in @("torch\include", "torch\test")) {
    $p = Join-Path $sitePackages $d
    if (Test-Path $p) { Remove-Item -Recurse -Force $p }
}
Get-ChildItem (Join-Path $sitePackages "torch\lib") -Include "*.lib", "*.pdb" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Force
if (Test-Path (Join-Path $work "python\Scripts")) { Remove-Item -Recurse -Force (Join-Path $work "python\Scripts") }

$extractedBytes = (Get-ChildItem $work -Recurse -File | Measure-Object Length -Sum).Sum
Write-Host ("Extracted size: {0:N1} GB" -f ($extractedBytes / 1GB))

# --- 9. Zip parts ---------------------------------------------------------
Write-Host "=== Zipping ==="
Get-ChildItem $outDir -Filter "$engineId-*" -ErrorAction SilentlyContinue | Remove-Item -Force
$allFiles = Get-ChildItem $work -Recurse -File | Sort-Object FullName
$weightFiles = $allFiles | Where-Object { $_.Extension -in ".safetensors", ".tar", ".pth" -and $_.Length -gt 50MB }
$runtimeFiles = $allFiles | Where-Object { $_ -notin $weightFiles }

function New-ZipParts {
    param([string]$Prefix, [object[]]$Files, [long]$RawCap, [bool]$Store)
    $part = 0; $current = $null; $sum = [long]0; $made = @()
    foreach ($f in $Files) {
        if ($null -eq $current -or ($sum + $f.Length) -gt $RawCap) {
            if ($current) { $current.Dispose() }
            $part++
            $zipPath = Join-Path $outDir "$Prefix-$part.zip"
            $made += $zipPath
            $current = [System.IO.Compression.ZipFile]::Open($zipPath, [System.IO.Compression.ZipArchiveMode]::Create)
            $sum = 0
        }
        $rel = $f.FullName.Substring($work.Length + 1) -replace "\\", "/"
        if ($rel.Length -gt 170) { throw "entry too long: $rel" }
        if ($Store) { $level = [System.IO.Compression.CompressionLevel]::NoCompression }
        else { $level = [System.IO.Compression.CompressionLevel]::Optimal }
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($current, $f.FullName, $rel, $level) | Out-Null
        $sum += $f.Length
    }
    if ($current) { $current.Dispose() }
    return $made
}

$parts = @()
$parts += New-ZipParts "$engineId-v1-runtime" $runtimeFiles ([long]3.4GB) $false
$parts += New-ZipParts "$engineId-v1-weights" $weightFiles ([long]1.7GB) $true
foreach ($p in $parts) {
    $len = (Get-Item $p).Length
    if ($len -gt 1.9GB) { throw "part too large: $p ($([math]::Round($len/1GB,2)) GB) - adjust caps" }
    Write-Host ("  {0}  {1:N1} MB" -f (Split-Path $p -Leaf), ($len / 1MB))
}

# --- 10. Manifest ---------------------------------------------------------
$manifestFiles = @()
foreach ($p in $parts) {
    $item = Get-Item $p
    $sha = (Get-FileHash $p -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestFiles += [ordered]@{
        fileName = $item.Name
        sha256 = $sha
        sizeBytes = $item.Length
        urls = @("https://github.com/MichaelSchaapDev/royal-news-desk/releases/download/$releaseTag/$($item.Name)")
        extract = $true
    }
}
$manifest = [ordered]@{
    engineId = $engineId
    sourceCommit = $sadTalkerCommit
    extractedSizeBytes = $extractedBytes
    files = $manifestFiles
}
$manifestPath = Join-Path $outDir "$engineId.manifest.json"
$manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestPath -Encoding utf8
Write-Host "Manifest: $manifestPath"

# --- 11. Upload -----------------------------------------------------------
if ($Upload) {
    Write-Host "=== Uploading to release $releaseTag ==="
    gh release view $releaseTag --repo MichaelSchaapDev/royal-news-desk 2>$null
    if ($LASTEXITCODE -ne 0) {
        gh release create $releaseTag --repo MichaelSchaapDev/royal-news-desk --title "Presenter engines v1" --notes "SadTalker presenter engine bundles downloaded by the app on demand. Not a program release."
    }
    foreach ($p in $parts) {
        gh release upload $releaseTag $p --repo MichaelSchaapDev/royal-news-desk --clobber
        if ($LASTEXITCODE -ne 0) { throw "upload failed for $p" }
    }
}
Write-Host "Done."
