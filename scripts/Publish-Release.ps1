param([Parameter(Mandatory = $true)][string]$Tag)
$ErrorActionPreference = "Stop"
if ($Tag -notmatch '^v(\d{1,4})\.(\d{1,2})\.(\d{1,2})$') { throw "Use uma tag vMAJOR.MINOR.PATCH." }
$releaseVersion = $Tag.Substring(1)
$releaseCode = [int]$Matches[1] * 10000 + [int]$Matches[2] * 100 + [int]$Matches[3]
$root = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $root
if (-not $env:ANDROID_KEYSTORE_BASE64 -or -not $env:ANDROID_SIGNING_PASSWORD) { throw "Configure os secrets ANDROID_KEYSTORE_BASE64 e ANDROID_SIGNING_PASSWORD." }
$keyPath = Join-Path $env:RUNNER_TEMP "mdreader-release.keystore"
[IO.File]::WriteAllBytes($keyPath, [Convert]::FromBase64String($env:ANDROID_KEYSTORE_BASE64))
try {
    & (Join-Path $root "publish.ps1") -Version $releaseVersion
    & (Join-Path $root "publish-android.ps1") -VersionName $releaseVersion -VersionCode $releaseCode `
        -AndroidSdkDirectory $env:ANDROID_HOME -JavaSdkDirectory $env:JAVA_HOME `
        -SigningKeyStorePath $keyPath -SigningPasswordEnvironmentName "ANDROID_SIGNING_PASSWORD"
    $releaseFolder = Join-Path $env:RUNNER_TEMP "mdreader-release"
    New-Item -ItemType Directory -Path $releaseFolder -Force | Out-Null
    Copy-Item -LiteralPath "$root\dist\MDReader.exe" -Destination $releaseFolder -Force
    Copy-Item -LiteralPath "$root\dist-android\MDReader.apk" -Destination $releaseFolder -Force
    $checksums = foreach ($file in Get-ChildItem -LiteralPath $releaseFolder -File) {
        "$((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant())  $($file.Name)"
    }
    [IO.File]::WriteAllLines((Join-Path $releaseFolder "SHA256SUMS"), $checksums, [Text.UTF8Encoding]::new($false))
    gh release view $Tag --repo $env:GITHUB_REPOSITORY 2>$null
    if ($LASTEXITCODE -ne 0) {
        gh release create $Tag --repo $env:GITHUB_REPOSITORY --verify-tag --title "MDReader $releaseVersion" --notes-file "$root\docs\release-notes.md" --latest
        if ($LASTEXITCODE -ne 0) { throw "Falha ao criar a Release." }
    }
    gh release upload $Tag "$releaseFolder\MDReader.apk" "$releaseFolder\MDReader.exe" "$releaseFolder\SHA256SUMS" --repo $env:GITHUB_REPOSITORY --clobber
    if ($LASTEXITCODE -ne 0) { throw "Falha ao publicar os arquivos." }
    git fetch origin master
    $downloadsCheckout = Join-Path $env:RUNNER_TEMP "mdreader-downloads"
    git worktree add --detach $downloadsCheckout origin/master
    if ($LASTEXITCODE -ne 0) { throw "Falha ao preparar a pasta de downloads." }
    & "$root\scripts\Update-Downloads.ps1" -AndroidApk "$releaseFolder\MDReader.apk" -WindowsExe "$releaseFolder\MDReader.exe" -Version $releaseVersion -RepositoryPath $downloadsCheckout
    git -C $downloadsCheckout config user.name "github-actions[bot]"
    git -C $downloadsCheckout config user.email "41898282+github-actions[bot]@users.noreply.github.com"
    git -C $downloadsCheckout add downloads
    git -C $downloadsCheckout diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        git -C $downloadsCheckout commit -m "dist: update downloads for $Tag"
        git -C $downloadsCheckout push origin HEAD:master
        if ($LASTEXITCODE -ne 0) { throw "Release publicada; falha ao atualizar a pasta downloads." }
    }
} finally { Remove-Item -LiteralPath $keyPath -Force -ErrorAction SilentlyContinue }
