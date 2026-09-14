param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^v?\d+\.\d+\.\d+([-.][0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [switch]$Publish,
    [switch]$Draft
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Keep this file ASCII-only for Windows PowerShell 5.1 compatibility.
$Root = $PSScriptRoot
$Project = Join-Path $Root 'MoverGUI\MoverGUI.csproj'
$Dist = Join-Path $Root 'dist'
$Temp = Join-Path $Root '.release-temp'

$CleanVersion = $Version.TrimStart('v')
$Tag = "v$CleanVersion"
$BaseName = "MoverGUI-$Tag-win-x64"
$PublishDir = Join-Path $Temp 'publish'
$PackageDir = Join-Path $Temp $BaseName
$ExeSource = Join-Path $PublishDir 'MoverGUI.exe'
$ExeAsset = Join-Path $Dist "$BaseName.exe"
$ZipAsset = Join-Path $Dist "$BaseName.zip"
$Checksums = Join-Path $Dist 'SHA256SUMS.txt'

function Require-Command([string]$Name) {
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command was not found: $Name"
    }
}

function Invoke-Checked([string]$File, [string[]]$Arguments) {
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$File failed with exit code $LASTEXITCODE"
    }
}

Write-Host '========================================' -ForegroundColor Cyan
Write-Host 'Mover GUI Release Builder' -ForegroundColor Cyan
Write-Host "Version: $CleanVersion" -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan

Require-Command 'dotnet'

if (-not (Test-Path $Project)) {
    throw "Project file was not found: $Project"
}

if (Test-Path $Temp) {
    Remove-Item $Temp -Recurse -Force
}
if (Test-Path $Dist) {
    Remove-Item $Dist -Recurse -Force
}

New-Item -ItemType Directory -Path $PublishDir -Force | Out-Null
New-Item -ItemType Directory -Path $PackageDir -Force | Out-Null
New-Item -ItemType Directory -Path $Dist -Force | Out-Null

Write-Host ''
Write-Host '[1/5] Building application...'

$PublishArgs = @(
    'publish', $Project,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:PublishReadyToRun=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    "-p:Version=$CleanVersion",
    '-o', $PublishDir
)

Invoke-Checked 'dotnet' $PublishArgs

if (-not (Test-Path $ExeSource)) {
    throw "Executable was not found: $ExeSource"
}

Write-Host '[2/5] Preparing release files...'
Copy-Item $ExeSource $ExeAsset -Force
Copy-Item $ExeSource (Join-Path $PackageDir 'MoverGUI.exe') -Force

$Readme = Join-Path $Root 'README.md'
$ThirdParty = Join-Path $Root 'THIRD_PARTY_NOTICES.md'
if (Test-Path $Readme) {
    Copy-Item $Readme (Join-Path $PackageDir 'README.md') -Force
}
if (Test-Path $ThirdParty) {
    Copy-Item $ThirdParty (Join-Path $PackageDir 'THIRD_PARTY_NOTICES.md') -Force
}

Write-Host '[3/5] Creating ZIP package...'
Compress-Archive -Path (Join-Path $PackageDir '*') -DestinationPath $ZipAsset -CompressionLevel Optimal -Force

Write-Host '[4/5] Calculating SHA256...'
$HashExe = (Get-FileHash $ExeAsset -Algorithm SHA256).Hash.ToLowerInvariant()
$HashZip = (Get-FileHash $ZipAsset -Algorithm SHA256).Hash.ToLowerInvariant()
@(
    "$HashExe  $([IO.Path]::GetFileName($ExeAsset))",
    "$HashZip  $([IO.Path]::GetFileName($ZipAsset))"
) | Set-Content -Path $Checksums -Encoding ASCII

Remove-Item $Temp -Recurse -Force

Write-Host '[5/5] Release files are ready.' -ForegroundColor Green
Write-Host ''
Write-Host $ExeAsset
Write-Host $ZipAsset
Write-Host $Checksums

if ($Publish) {
    Write-Host ''
    Write-Host 'Publishing GitHub Release...' -ForegroundColor Cyan

    Require-Command 'git'
    Require-Command 'gh'

    Invoke-Checked 'gh' @('auth', 'status')
    Invoke-Checked 'git' @('rev-parse', '--is-inside-work-tree')

    $Dirty = (& git status --porcelain)
    if ($LASTEXITCODE -ne 0) {
        throw 'git status failed.'
    }
    if ($Dirty) {
        throw 'Working tree is not clean. Commit or stash changes before publishing a release.'
    }

    & git rev-parse --verify --quiet "refs/tags/$Tag" | Out-Null
    if ($LASTEXITCODE -eq 0) {
        throw "Local tag already exists: $Tag"
    }

    Invoke-Checked 'git' @('tag', '-a', $Tag, '-m', "Mover GUI $CleanVersion")
    Invoke-Checked 'git' @('push', 'origin', $Tag)

    $NotesFile = Join-Path $Root "release-notes\$Tag.md"
    $ReleaseArgs = @(
        'release', 'create', $Tag,
        $ExeAsset,
        $ZipAsset,
        $Checksums,
        '--title', "Mover GUI $CleanVersion",
        '--verify-tag'
    )

    if ($Draft) {
        $ReleaseArgs += '--draft'
    }

    if (Test-Path $NotesFile) {
        $ReleaseArgs += @('--notes-file', $NotesFile)
    }
    else {
        $ReleaseArgs += '--generate-notes'
    }

    Invoke-Checked 'gh' $ReleaseArgs

    Write-Host ''
    Write-Host "GitHub Release $Tag was created successfully." -ForegroundColor Green
}
