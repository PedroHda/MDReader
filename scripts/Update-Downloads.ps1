param(
    [Parameter(Mandatory = $true)][string]$AndroidApk,
    [Parameter(Mandatory = $true)][string]$WindowsExe,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = "1.1.0",
    [string]$RepositoryPath = (Split-Path -Parent $PSScriptRoot)
)
$ErrorActionPreference = "Stop"
foreach ($download in @(
    @{ Platform = "android"; Source = $AndroidApk; Name = "MDReader.apk" },
    @{ Platform = "windows"; Source = $WindowsExe; Name = "MDReader.exe" }
)) {
    $folder = Join-Path $RepositoryPath ("downloads\" + $download.Platform)
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $destination = Join-Path $folder $download.Name
    Copy-Item -LiteralPath $download.Source -Destination $destination -Force
    $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
    $encoding = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText((Join-Path $folder "SHA256SUMS"), "$hash  $($download.Name)`n", $encoding)
    $manifest = @{ version = $Version; file = $download.Name; sha256 = $hash; bytes = (Get-Item -LiteralPath $destination).Length }
    [IO.File]::WriteAllText((Join-Path $folder "version.json"), ($manifest | ConvertTo-Json), $encoding)
}
