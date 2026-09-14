$ErrorActionPreference = 'Stop'

$Project = Join-Path $PSScriptRoot 'MoverGUI\MoverGUI.csproj'
$Out = Join-Path $PSScriptRoot 'publish'

Write-Host '=== Mover GUI build ===' -ForegroundColor Cyan

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host ''
    Write-Host '.NET 8 SDK was not found.' -ForegroundColor Yellow
    Write-Host 'Install .NET 8 SDK x64 and run Build_EXE.cmd again.'
    Write-Host 'Check installation with: dotnet --version'
    exit 1
}

if (-not (Test-Path $Project)) {
    Write-Host ''
    Write-Host 'Project file was not found:' -ForegroundColor Red
    Write-Host $Project
    exit 1
}

if (Test-Path $Out) {
    Remove-Item $Out -Recurse -Force
}

Write-Host ''
Write-Host 'Publishing win-x64 self-contained executable...'

dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $Out

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$Exe = Join-Path $Out 'MoverGUI.exe'
if (-not (Test-Path $Exe)) {
    throw "Build finished but executable was not found: $Exe"
}

Write-Host ''
Write-Host 'Build completed successfully:' -ForegroundColor Green
Write-Host $Exe
