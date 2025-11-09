# Bundle language server for all supported platforms
# Creates platform-specific server binaries for distribution

param(
    [string]$Configuration = "Release"
)

$platforms = @(
    "win-x64",      # Windows 64-bit
    "win-arm64",    # Windows ARM64
    "linux-x64",    # Linux 64-bit
    "linux-arm64",  # Linux ARM64
    "osx-x64",      # macOS Intel
    "osx-arm64"     # macOS Apple Silicon
)

Write-Host "Bundling SharpFocus Language Server for all platforms..." -ForegroundColor Cyan
Write-Host ""

$rootDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$serverProject = Join-Path $rootDir "src\SharpFocus.LanguageServer\SharpFocus.LanguageServer.csproj"
$baseOutputDir = Join-Path $PSScriptRoot "..\server"

# Clean previous builds
if (Test-Path $baseOutputDir) {
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    Remove-Item $baseOutputDir -Recurse -Force
}

foreach ($runtime in $platforms) {
    Write-Host "Publishing for $runtime..." -ForegroundColor Yellow

    $outputDir = Join-Path $baseOutputDir $runtime

    dotnet publish $serverProject `
        --configuration $Configuration `
        --runtime $runtime `
        --self-contained false `
        --output $outputDir `
        /p:PublishSingleFile=false

    if ($LASTEXITCODE -eq 0) {
        $size = (Get-ChildItem $outputDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
        Write-Host "  ✓ Published $runtime ($([math]::Round($size, 2)) MB)" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Failed to publish $runtime" -ForegroundColor Red
    }
    Write-Host ""
}

Write-Host "All platforms bundled successfully!" -ForegroundColor Cyan
Write-Host "Output directory: $baseOutputDir" -ForegroundColor Gray
