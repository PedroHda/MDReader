$ErrorActionPreference = "Stop"

$rootPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $rootPath "MDReader\MDReader.csproj"
$distPath = Join-Path $rootPath "dist"

if (Test-Path -LiteralPath $distPath) {
    Remove-Item -LiteralPath $distPath -Recurse -Force
}

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $distPath `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true `
    /p:DebugType=None `
    /p:DebugSymbols=false

$debugSymbolsPath = Join-Path $distPath "MDReader.pdb"

if (Test-Path -LiteralPath $debugSymbolsPath) {
    Remove-Item -LiteralPath $debugSymbolsPath -Force
}

Write-Host ""
Write-Host "Executavel gerado em:"
Write-Host (Join-Path $distPath "MDReader.exe")
