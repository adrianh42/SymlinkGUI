param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "./dist",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

# Auto-resolve version if not explicitly passed
if (-not $Version) {
    if ($env:GITHUB_REF_NAME) {
        $Version = $env:GITHUB_REF_NAME
    } else {
        $propsPath = Join-Path $PSScriptRoot "Directory.Build.props"
        if (Test-Path $propsPath) {
            [xml]$propsXml = Get-Content $propsPath
            $Version = $propsXml.Project.PropertyGroup.Version
        }
    }
}

if ($Version) {
    $Version = $Version.TrimStart('v')
}

$baseName = if ($Version) { "SymlinkGUI-Portable-$Runtime-$Version" } else { "SymlinkGUI-Portable-$Runtime" }
$portableDir = Join-Path $OutputDir $baseName
$zipPath = Join-Path $OutputDir "$baseName.zip"

$versionDisplay = if ($Version) { $Version } else { "unversioned" }
Write-Host "Building portable package for $Runtime ($versionDisplay, framework-dependent)..." -ForegroundColor Cyan

# Ensure clean staging directory
if (Test-Path $portableDir) {
    Remove-Item -Recurse -Force $portableDir
}
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

$publishArgs = @(
    "publish", "src/SymlinkGUI/SymlinkGUI.csproj",
    "-c", "Release",
    "-r", $Runtime,
    "--self-contained", "false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false"
)

if ($Version) {
    $publishArgs += "-p:Version=$Version"
}

$publishArgs += @("-o", $portableDir)

dotnet @publishArgs

if ($LASTEXITCODE -eq 0) {
    $exePath = Join-Path $portableDir "SymlinkGUI.exe"
    if (Test-Path $exePath) {
        Write-Host "Creating zip archive: $zipPath..." -ForegroundColor Cyan
        Compress-Archive -Path "$portableDir\*" -DestinationPath $zipPath -Force

        $folderSizeMB = [math]::Round((Get-ChildItem -Path $portableDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB, 2)
        $zipSizeMB = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)

        Write-Host "Portable build completed successfully!" -ForegroundColor Green
        Write-Host "  Folder: $portableDir ($folderSizeMB MB)" -ForegroundColor Green
        Write-Host "  Zip:    $zipPath ($zipSizeMB MB)" -ForegroundColor Green
    }
}
