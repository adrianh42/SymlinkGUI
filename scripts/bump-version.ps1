<#
.SYNOPSIS
    Bumps or sets the project version in Directory.Build.props.

.DESCRIPTION
    Increments the Semantic Version (patch, minor, or major) in Directory.Build.props,
    or explicitly sets a specific version number. Optionally creates a git commit and tag.

.PARAMETER Type
    The version increment type: 'patch', 'minor', or 'major'. Default is 'patch'.

.PARAMETER Set
    Sets an explicit version string (e.g. '1.3.0'), overriding the Type parameter.

.PARAMETER Commit
    Stages Directory.Build.props and commits the version bump with a conventional commit message.

.PARAMETER Tag
    Creates an annotated git tag 'v<new_version>'.

.PARAMETER Push
    Pushes the commit and tags to the remote git repository.

.EXAMPLE
    .\scripts\bump-version.ps1 -Type patch
    .\scripts\bump-version.ps1 -Type minor
    .\scripts\bump-version.ps1 -Type major
    .\scripts\bump-version.ps1 -Set 1.3.0
    .\scripts\bump-version.ps1 -Type patch -Commit -Tag
#>

param(
    [ValidateSet("patch", "minor", "major")]
    [string]$Type = "patch",

    [string]$Set = "",

    [switch]$Commit,
    [switch]$Tag,
    [switch]$Push,

    [string]$PropsPath = ""
)

$ErrorActionPreference = "Stop"

if (-not $PropsPath) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $PropsPath = Join-Path $repoRoot "Directory.Build.props"
}

if (-not (Test-Path $PropsPath)) {
    Write-Error "Could not find Directory.Build.props at: $PropsPath"
    exit 1
}

$content = Get-Content -Raw $PropsPath

if ($content -notmatch '<Version>(.*?)</Version>') {
    Write-Error "Could not find <Version> tag in $PropsPath"
    exit 1
}

$oldVersion = $Matches[1].Trim()

# Calculate new version
$newVersion = ""

if ($Set) {
    $cleanSet = $Set.TrimStart('v')
    if ($cleanSet -notmatch '^\d+\.\d+\.\d+$') {
        Write-Error "Invalid version format '$Set'. Expected 'X.Y.Z' (e.g., 1.2.3)."
        exit 1
    }
    $newVersion = $cleanSet
} else {
    if ($oldVersion -notmatch '^(\d+)\.(\d+)\.(\d+)$') {
        Write-Error "Current version '$oldVersion' is not in standard 'X.Y.Z' format."
        exit 1
    }

    [int]$major = [int]$Matches[1]
    [int]$minor = [int]$Matches[2]
    [int]$patch = [int]$Matches[3]

    switch ($Type) {
        "patch" {
            $patch++
        }
        "minor" {
            $minor++
            $patch = 0
        }
        "major" {
            $major++
            $minor = 0
            $patch = 0
        }
    }

    $newVersion = "$major.$minor.$patch"
}

if ($oldVersion -eq $newVersion) {
    Write-Host "Version is already $newVersion. No changes made." -ForegroundColor Yellow
    exit 0
}

# Update Directory.Build.props
$updatedContent = $content -replace "<Version>$([regex]::Escape($oldVersion))</Version>", "<Version>$newVersion</Version>"
Set-Content -Path $PropsPath -Value $updatedContent -NoNewline

Write-Host "Successfully bumped version: $oldVersion -> $newVersion in Directory.Build.props" -ForegroundColor Green

# Optional Git integration
if ($Commit) {
    Write-Host "Creating git commit..." -ForegroundColor Cyan
    git add $PropsPath
    git commit -m "chore(release): bump version to $newVersion"
}

if ($Tag) {
    Write-Host "Creating git tag v$newVersion..." -ForegroundColor Cyan
    git tag "v$newVersion"
}

if ($Push) {
    Write-Host "Pushing changes..." -ForegroundColor Cyan
    git push
    if ($Tag) {
        git push --tags
    }
}
