param(
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "./dist"
)

$ErrorActionPreference = "Stop"

$portableDir = Join-Path $OutputDir "SymlinkGUI-Portable-$Runtime"
$zipPath = Join-Path $OutputDir "SymlinkGUI-Portable-$Runtime.zip"

Write-Host "Building portable package for $Runtime (framework-dependent)..." -ForegroundColor Cyan

# Ensure clean staging directory
if (Test-Path $portableDir) {
    Remove-Item -Recurse -Force $portableDir
}
if (Test-Path $zipPath) {
    Remove-Item -Force $zipPath
}

dotnet publish src/SymlinkGUI/SymlinkGUI.csproj `
    -c Release `
    -r $Runtime `
    --self-contained false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $portableDir

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
