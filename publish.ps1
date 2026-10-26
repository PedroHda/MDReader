param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = "1.1.0")
$ErrorActionPreference = "Stop"
$rootPath = $PSScriptRoot
$projectPath = Join-Path $rootPath "MDReader\MDReader.csproj"
$distPath = Join-Path $rootPath "dist"
dotnet publish $projectPath -c Release -r win-x64 --self-contained true -o $distPath `
    /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:EnableCompressionInSingleFile=true /p:DebugType=None /p:DebugSymbols=false "-p:Version=$Version"
if ($LASTEXITCODE -ne 0) { throw "Falha na publicacao Windows." }
Write-Host ""
Write-Host "Executavel gerado em:"
Write-Host (Join-Path $distPath "MDReader.exe")
