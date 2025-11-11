#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Package SharpFocus Rider Plugin for JetBrains Marketplace

.DESCRIPTION
    Builds the plugin with all dependencies and language server binaries
    for distribution on JetBrains Marketplace.

.PARAMETER SkipTests
    Skip running tests during build

.PARAMETER Clean
    Clean build directories before building

.EXAMPLE
    .\package-plugin.ps1
    .\package-plugin.ps1 -Clean
#>

param(
    [switch]$SkipTests = $false,
    [switch]$Clean = $false
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "SharpFocus Rider Plugin - Package" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
Write-Host "🔍 Checking prerequisites..." -ForegroundColor Yellow

# Check Java
$javaVersion = & java -version 2>&1 | Select-String "version" | ForEach-Object { $_ -replace '.*version "([^"]+)".*', '$1' }
if ($javaVersion -notmatch "^(21|2[2-9]|[3-9]\d)\.") {
    Write-Host "❌ Java 21 or higher required. Found: $javaVersion" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Java version: $javaVersion" -ForegroundColor Green

# Check .NET SDK
$dotnetVersion = & dotnet --version 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ .NET SDK not found" -ForegroundColor Red
    exit 1
}
Write-Host "✓ .NET SDK version: $dotnetVersion" -ForegroundColor Green

Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "🧹 Cleaning build directories..." -ForegroundColor Yellow
    & .\gradlew.bat clean
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Clean failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "✓ Clean complete" -ForegroundColor Green
    Write-Host ""
}

# Build plugin (Gradle will automatically build the language server via bundleLanguageServer task)
Write-Host "📦 Building plugin distribution..." -ForegroundColor Yellow
Write-Host "   (Language server will be built automatically for all platforms)" -ForegroundColor Gray

$gradleArgs = @("buildPlugin")
if ($SkipTests) {
    $gradleArgs += "-x", "test"
}

& .\gradlew.bat $gradleArgs

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Plugin build failed" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Plugin built successfully" -ForegroundColor Green
Write-Host ""

# Verify output
$distDir = Join-Path $PSScriptRoot "build\distributions"
$pluginZip = Get-ChildItem -Path $distDir -Filter "*sharpfocus*.zip" | Select-Object -First 1

if (-not $pluginZip) {
    Write-Host "❌ Plugin distribution not found in: $distDir" -ForegroundColor Red
    exit 1
}

$zipPath = $pluginZip.FullName
$zipSize = [math]::Round($pluginZip.Length / 1MB, 2)

Write-Host "========================================" -ForegroundColor Green
Write-Host "✅ Package Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "📦 Distribution Package:" -ForegroundColor Cyan
Write-Host "   Location: $zipPath" -ForegroundColor White
Write-Host "   Size: $zipSize MB" -ForegroundColor White
Write-Host ""

# Verify contents
Write-Host "📋 Package Contents:" -ForegroundColor Cyan
Add-Type -AssemblyName System.IO.Compression.FileSystem
try {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)

    # Check for plugin.xml
    $pluginXml = $zip.Entries | Where-Object { $_.FullName -like "*/plugin.xml" }
    if ($pluginXml) {
        Write-Host "   ✓ plugin.xml" -ForegroundColor Green
    }

    # Check for server binaries (they're inside the plugin JAR)
    $serverPlatforms = @("win-x64", "linux-x64", "osx-x64", "osx-arm64")
    $foundPlatforms = @()

    # Find the main plugin JAR
    $pluginJar = $zip.Entries | Where-Object { $_.FullName -like "*/lib/*sharpfocus*.jar" -and $_.FullName -notlike "*-searchableOptions.jar" } | Select-Object -First 1

    if ($pluginJar) {
        # Extract the JAR to temp and check inside it
        $tempJar = [System.IO.Path]::GetTempFileName()
        try {
            $stream = $pluginJar.Open()
            $fileStream = [System.IO.File]::OpenWrite($tempJar)
            $stream.CopyTo($fileStream)
            $fileStream.Close()
            $stream.Close()

            $jar = [System.IO.Compression.ZipFile]::OpenRead($tempJar)
            foreach ($platform in $serverPlatforms) {
                $serverBinary = $jar.Entries | Where-Object { $_.FullName -like "server/$platform/*LanguageServer*" -and $_.Length -gt 1MB }
                if ($serverBinary) {
                    $foundPlatforms += $platform
                }
            }
            $jar.Dispose()
        } finally {
            if (Test-Path $tempJar) {
                Remove-Item $tempJar -Force -ErrorAction SilentlyContinue
            }
        }
    }

    if ($foundPlatforms.Count -gt 0) {
        Write-Host "   ✓ Language server binaries: $($foundPlatforms -join ', ')" -ForegroundColor Green
    } else {
        Write-Host "   ⚠ No language server binaries found" -ForegroundColor Yellow
    }

    # Check for icons
    $icons = $zip.Entries | Where-Object { $_.FullName -like "*/icons/*.svg" }
    if ($icons) {
        Write-Host "   ✓ Icons ($($icons.Count) files)" -ForegroundColor Green
    }

    $zip.Dispose()
} catch {
    Write-Host "   ⚠ Could not verify package contents: $_" -ForegroundColor Yellow
}

