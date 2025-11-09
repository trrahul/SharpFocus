# Bundle .NET Language Server with VS Code Extension
# This script publishes the language server as a self-contained deployment

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

Write-Host "Bundling SharpFocus Language Server..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Runtime: $Runtime" -ForegroundColor Gray
Write-Host ""

# Paths
$rootDir = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$serverProject = Join-Path $rootDir "src\SharpFocus.LanguageServer\SharpFocus.LanguageServer.csproj"
$outputDir = Join-Path $PSScriptRoot "..\server\$Runtime"

# Validate project exists
if (-not (Test-Path $serverProject)) {
    Write-Host "ERROR: Language server project not found at: $serverProject" -ForegroundColor Red
    exit 1
}

Write-Host "Publishing language server..." -ForegroundColor Yellow

# Publish for specified runtime
# --self-contained false means it requires .NET runtime installed
# For fully self-contained (no .NET required), use --self-contained true (but much larger)
dotnet publish $serverProject `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained false `
    --output $outputDir `
    /p:PublishSingleFile=false `
    /p:EnableCompressionInSingleFile=false

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Publish failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "✓ Language server bundled to: $outputDir" -ForegroundColor Green
Write-Host ""
Write-Host "Files included:" -ForegroundColor Gray
Get-ChildItem $outputDir -File | Select-Object Name, @{Name="Size (KB)";Expression={[math]::Round($_.Length/1KB,2)}} | Format-Table -AutoSize

$totalSize = (Get-ChildItem $outputDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "Total size: $([math]::Round($totalSize, 2)) MB" -ForegroundColor Gray
