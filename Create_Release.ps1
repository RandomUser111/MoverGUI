param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [switch]$Publish,

    [switch]$Draft
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root "MoverGUI\MoverGUI.csproj"

$DistDir = Join-Path $Root "dist"
$WorkDir = Join-Path $Root ".release"
$PublishDir = Join-Path $WorkDir "publish"
$PackageDir = Join-Path $WorkDir "package"

$Tag = "v$Version"

$ExeFileName = "MoverGUI-v$Version-win-x64.exe"
$ZipFileName = "MoverGUI-v$Version-win-x64.zip"

$ExeFile = Join-Path $DistDir $ExeFileName
$ZipFile = Join-Path $DistDir $ZipFileName
$HashFile = Join-Path $DistDir "SHA256SUMS.txt"

Write-Host ""
Write-Host "========================================="
Write-Host " MoverGUI Release Builder"
Write-Host " Version: $Version"
Write-Host "========================================="
Write-Host ""

if (-not (Test-Path $Project)) {
    throw "Project not found: $Project"
}

$DotNet = Get-Command dotnet -ErrorAction SilentlyContinue

if (-not $DotNet) {
    throw ".NET SDK was not found."
}

Write-Host "[1/6] Checking .NET SDK..."

dotnet --version

if (Test-Path $DistDir) {
    Remove-Item $DistDir -Recurse -Force
}

if (Test-Path $WorkDir) {
    Remove-Item $WorkDir -Recurse -Force
}

New-Item -ItemType Directory -Path $DistDir | Out-Null
New-Item -ItemType Directory -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Path $PackageDir | Out-Null

Write-Host ""
Write-Host "[2/6] Building application..."

dotnet publish $Project `
    -c Release `
    -r win-x64 `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:PublishReadyToRun=false `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$Version `
    -o $PublishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed."
}

$PublishedExe = Join-Path $PublishDir "MoverGUI.exe"

if (-not (Test-Path $PublishedExe)) {
    throw "MoverGUI.exe was not created."
}

Write-Host ""
Write-Host "[3/6] Preparing release files..."

Copy-Item $PublishedExe $ExeFile

Copy-Item $PublishedExe (Join-Path $PackageDir "MoverGUI.exe")

$Readme = Join-Path $Root "README.md"

if (Test-Path $Readme) {
    Copy-Item $Readme $PackageDir
}

$ThirdParty = Join-Path $Root "THIRD_PARTY_NOTICES.md"

if (Test-Path $ThirdParty) {
    Copy-Item $ThirdParty $PackageDir
}

Write-Host ""
Write-Host "[4/6] Creating ZIP package..."

Compress-Archive `
    -Path (Join-Path $PackageDir "*") `
    -DestinationPath $ZipFile `
    -CompressionLevel Optimal

Write-Host ""
Write-Host "[5/6] Calculating SHA256..."

$ExeHash = (Get-FileHash $ExeFile -Algorithm SHA256).Hash.ToLower()
$ZipHash = (Get-FileHash $ZipFile -Algorithm SHA256).Hash.ToLower()

@"
$ExeHash  $ExeFileName
$ZipHash  $ZipFileName
"@ | Set-Content $HashFile -Encoding ASCII

$ExeSize = [math]::Round((Get-Item $ExeFile).Length / 1MB, 2)
$ZipSize = [math]::Round((Get-Item $ZipFile).Length / 1MB, 2)

Write-Host ""
Write-Host "[6/6] Release files created."
Write-Host ""
Write-Host "EXE : $ExeFileName ($ExeSize MB)"
Write-Host "ZIP : $ZipFileName ($ZipSize MB)"
Write-Host "HASH: SHA256SUMS.txt"

if ($Publish) {

    Write-Host ""
    Write-Host "========================================="
    Write-Host " Publishing GitHub Release"
    Write-Host "========================================="
    Write-Host ""

    $Git = Get-Command git -ErrorAction SilentlyContinue

    if (-not $Git) {
        throw "Git was not found."
    }

    $Gh = Get-Command gh -ErrorAction SilentlyContinue

    if (-not $Gh) {
        throw "GitHub CLI (gh) was not found."
    }

    Push-Location $Root

    try {

        Write-Host "Checking GitHub authentication..."

        gh auth status

        if ($LASTEXITCODE -ne 0) {
            throw "GitHub CLI is not authenticated."
        }

        Write-Host ""
        Write-Host "Checking repository status..."

        $GitStatus = git status --porcelain

        if ($GitStatus) {
            Write-Host ""
            Write-Host "Working tree is not clean:"
            Write-Host $GitStatus
            Write-Host ""
            throw "Commit and push your changes before publishing a release."
        }

        $ExistingTag = git tag --list $Tag

        if (-not $ExistingTag) {

            Write-Host ""
            Write-Host "Creating tag $Tag..."

            git tag -a $Tag -m "MoverGUI $Version"

            if ($LASTEXITCODE -ne 0) {
                throw "Unable to create Git tag."
            }

            Write-Host "Pushing tag to GitHub..."

            git push origin $Tag

            if ($LASTEXITCODE -ne 0) {
                throw "Unable to push Git tag."
            }
        }
        else {
            Write-Host "Tag $Tag already exists."
        }

        $NotesFile = Join-Path $Root "release-notes\$Tag.md"

        $ReleaseArgs = @(
            "release",
            "create",
            $Tag,
            $ExeFile,
            $ZipFile,
            $HashFile,
            "--title",
            "MoverGUI $Version"
        )

        if (Test-Path $NotesFile) {
            $ReleaseArgs += "--notes-file"
            $ReleaseArgs += $NotesFile
        }
        else {
            $ReleaseArgs += "--generate-notes"
        }

        if ($Draft) {
            $ReleaseArgs += "--draft"
        }

        Write-Host ""
        Write-Host "Creating GitHub Release..."

        & gh @ReleaseArgs

        if ($LASTEXITCODE -ne 0) {
            throw "GitHub Release creation failed."
        }

        Write-Host ""
        Write-Host "GitHub Release created successfully."
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "========================================="
Write-Host " Done"
Write-Host "========================================="