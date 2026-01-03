#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build script for ImageConverterLibrary ODC External Logic project

.DESCRIPTION
    This script performs the following steps:
    1. Restores NuGet packages
    2. Builds the project
    3. Publishes for linux-x64 runtime (ODC Lambda requirement)
    4. Creates a deployment ZIP file

.PARAMETER Clean
    If specified, cleans the build output before building

.EXAMPLE
    .\build.ps1
    .\build.ps1 -Clean
#>

param(
    [switch]$Clean
)

# Set error action preference
$ErrorActionPreference = "Stop"

# Configuration
$projectFile = "ImageConverterLibrary.csproj"
$configuration = "Release"
$runtime = "linux-x64"
$publishDir = "bin\publish"
$zipFile = "ImageConverterLibrary.zip"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ImageConverterLibrary Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "[1/4] Cleaning previous build outputs..." -ForegroundColor Yellow
    if (Test-Path "bin") { Remove-Item -Path "bin" -Recurse -Force }
    if (Test-Path "obj") { Remove-Item -Path "obj" -Recurse -Force }
    if (Test-Path $zipFile) { Remove-Item -Path $zipFile -Force }
    Write-Host "      Clean completed" -ForegroundColor Green
} else {
    Write-Host "[1/4] Skipping clean (use -Clean to clean first)" -ForegroundColor Gray
}

# Restore packages
Write-Host ""
Write-Host "[2/4] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $projectFile
if ($LASTEXITCODE -ne 0) {
    Write-Host "      ERROR: NuGet restore failed" -ForegroundColor Red
    exit 1
}
Write-Host "      Restore completed" -ForegroundColor Green

# Build the project
Write-Host ""
Write-Host "[3/4] Building project..." -ForegroundColor Yellow
dotnet build $projectFile --configuration $configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Host "      ERROR: Build failed" -ForegroundColor Red
    exit 1
}
Write-Host "      Build completed" -ForegroundColor Green

# Publish for ODC Lambda (linux-x64)
Write-Host ""
Write-Host "[4/4] Publishing for ODC Lambda deployment..." -ForegroundColor Yellow
dotnet publish $projectFile `
    --configuration $configuration `
    --runtime $runtime `
    --output $publishDir `
    --no-build `
    --self-contained true

if ($LASTEXITCODE -ne 0) {
    Write-Host "      ERROR: Publish failed" -ForegroundColor Red
    exit 1
}
Write-Host "      Publish completed" -ForegroundColor Green

# Create deployment ZIP
Write-Host ""
Write-Host "[5/5] Creating deployment ZIP..." -ForegroundColor Yellow

# Remove old ZIP if exists
if (Test-Path $zipFile) {
    Remove-Item -Path $zipFile -Force
}

# Create ZIP from publish directory
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipFile -CompressionLevel Optimal

if (Test-Path $zipFile) {
    $zipSize = (Get-Item $zipFile).Length / 1MB
    Write-Host "      ZIP created: $zipFile ($("{0:N2}" -f $zipSize) MB)" -ForegroundColor Green
} else {
    Write-Host "      ERROR: Failed to create ZIP file" -ForegroundColor Red
    exit 1
}

# Summary
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "BUILD SUCCESSFUL!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Deployment package: $zipFile" -ForegroundColor White
Write-Host "Upload this ZIP to OutSystems ODC Library Manager" -ForegroundColor White
Write-Host ""
