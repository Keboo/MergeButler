#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Packages MergeButler and installs it as a local dotnet tool for testing.
.DESCRIPTION
    Builds and packs MergeButler as a dotnet tool, then installs it locally
    (replacing any existing installation). The tool can then be run via:
        dotnet mergebutler
.PARAMETER Configuration
    Build configuration (default: Release).
#>
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = $PSScriptRoot
$projectPath = Join-Path $repoRoot "MergeButler"
$nupkgDir = Join-Path $projectPath "nupkg"
$toolName = "MergeButler"

Write-Host "=== MergeButler Local Install ===" -ForegroundColor Cyan

# Clean previous package output
if (Test-Path $nupkgDir) {
    Write-Host "Cleaning previous packages..." -ForegroundColor Yellow
    Remove-Item $nupkgDir -Recurse -Force
}

# Pack as a dotnet tool
Write-Host "Packing $toolName..." -ForegroundColor Yellow
dotnet pack $projectPath `
    --configuration $Configuration `
    --output $nupkgDir `
    -p:PackAsTool=true `
    -p:PackageId=$toolName `
    -p:ToolCommandName=$toolName

if ($LASTEXITCODE -ne 0) {
    Write-Host "Pack failed." -ForegroundColor Red
    exit 1
}

# Find the generated .nupkg
$nupkg = Get-ChildItem $nupkgDir -Filter "*.nupkg" | Select-Object -First 1
if (-not $nupkg) {
    Write-Host "No .nupkg found in $nupkgDir" -ForegroundColor Red
    exit 1
}
Write-Host "Packed: $($nupkg.Name)" -ForegroundColor Green

# Uninstall existing local tool (ignore errors if not installed)
Write-Host "Uninstalling existing local tool (if any)..." -ForegroundColor Yellow
dotnet tool uninstall $toolName --local 2>$null

# Ensure a tool manifest exists
if (-not (Test-Path (Join-Path $repoRoot ".config" "dotnet-tools.json"))) {
    Write-Host "Creating tool manifest..." -ForegroundColor Yellow
    dotnet new tool-manifest --output $repoRoot 2>$null
}

# Install from the local package
Write-Host "Installing $toolName locally..." -ForegroundColor Yellow
dotnet tool install $toolName `
    --local `
    --add-source $nupkgDir `
    --version "*-*"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Install failed." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Installed successfully! ===" -ForegroundColor Green
Write-Host "Run with:  dotnet $toolName" -ForegroundColor Cyan
