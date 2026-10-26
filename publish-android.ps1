param(
    [ValidateRange(1, 2100000000)]
    [int]$VersionCode = 10100,
    [string]$VersionName = "1.1.0",
    [string]$AndroidSdkDirectory,
    [string]$JavaSdkDirectory,
    [string]$SigningKeyStorePath,
    [string]$SigningPasswordEnvironmentName
)

$ErrorActionPreference = "Stop"
$rootPath = $PSScriptRoot
$projectPath = Join-Path $rootPath "MDReader.Android\MDReader.Android.csproj"
$publishPath = Join-Path $rootPath "MDReader.Android\bin\apk-publish"
$distributionPath = Join-Path $rootPath "dist-android"
$signingPath = Join-Path $rootPath ".local-signing"
$keyStorePath = Join-Path $signingPath "mdreader.keystore"
if ($SigningKeyStorePath) { $keyStorePath = (Resolve-Path -LiteralPath $SigningKeyStorePath).Path }
$passwordPath = Join-Path $signingPath "android-password.clixml"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Instale o SDK .NET 10 e a carga de trabalho android antes de publicar."
}

if (-not $SigningPasswordEnvironmentName -and (Test-Path -LiteralPath $keyStorePath) -and -not (Test-Path -LiteralPath $passwordPath)) {
    throw "A senha da chave existente nao foi encontrada. Restaure android-password.clixml do seu backup."
}

New-Item -ItemType Directory -Path $signingPath -Force | Out-Null
$previousSigningPassword = $env:MDREADER_ANDROID_SIGNING_PASSWORD

try {
    if (-not $SigningPasswordEnvironmentName -and -not (Test-Path -LiteralPath $passwordPath)) {
        $randomBytes = New-Object byte[] 32
        $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $generator.GetBytes($randomBytes) } finally { $generator.Dispose() }
        $securePassword = ConvertTo-SecureString ([Convert]::ToBase64String($randomBytes)) -AsPlainText -Force
        $securePassword | Export-Clixml -LiteralPath $passwordPath
    }

    if ($SigningPasswordEnvironmentName) {
        $env:MDREADER_ANDROID_SIGNING_PASSWORD = [Environment]::GetEnvironmentVariable($SigningPasswordEnvironmentName)
        if (-not $env:MDREADER_ANDROID_SIGNING_PASSWORD -or -not (Test-Path -LiteralPath $keyStorePath)) {
            throw "Chave de assinatura ou senha do ambiente CI nao encontrada."
        }
    } else {
        $securePassword = Import-Clixml -LiteralPath $passwordPath
        $credential = New-Object System.Net.NetworkCredential("mdreader", $securePassword)
        $env:MDREADER_ANDROID_SIGNING_PASSWORD = $credential.Password
    }

    if (-not (Test-Path -LiteralPath $keyStorePath)) {
        $keyToolCommand = Get-Command keytool -ErrorAction SilentlyContinue
        $keyToolPath = if ($keyToolCommand) { $keyToolCommand.Source } else { $null }
        $javaCandidates = @($JavaSdkDirectory, $env:JAVA_HOME)
        $bundledJavaPath = Join-Path $env:ProgramFiles "Android\openjdk"
        if (Test-Path -LiteralPath $bundledJavaPath) {
            $javaCandidates += Get-ChildItem -LiteralPath $bundledJavaPath -Directory | Sort-Object Name -Descending | Select-Object -ExpandProperty FullName
        }

        foreach ($javaCandidate in $javaCandidates) {
            if ($javaCandidate) {
                $candidateTool = Join-Path $javaCandidate "bin\keytool.exe"
                if (Test-Path -LiteralPath $candidateTool) { $keyToolPath = $candidateTool; break }
            }
        }

        if (-not $keyToolPath) {
            throw "JDK nao encontrado. Configure JAVA_HOME ou informe -JavaSdkDirectory."
        }

        & $keyToolPath -genkeypair -keystore $keyStorePath -storetype JKS -alias mdreader `
            -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=MDReader" `
            -storepass:env MDREADER_ANDROID_SIGNING_PASSWORD -keypass:env MDREADER_ANDROID_SIGNING_PASSWORD -noprompt
        if ($LASTEXITCODE -ne 0) { throw "Falha ao criar a chave de assinatura do APK." }
    }

    $publishArguments = @(
        "publish", $projectPath, "-c", "Release", "-o", $publishPath,
        # Isolate signed releases so ordinary builds cannot supply a cached debug-signed APK.
        "-p:OutputPath=bin\apk-release\", "-p:IntermediateOutputPath=obj\apk-release\",
        "-p:AndroidPackageFormats=apk", "-p:AndroidKeyStore=true",
        "-p:AndroidSigningKeyStore=$keyStorePath", "-p:AndroidSigningKeyAlias=mdreader",
        "-p:AndroidSigningStorePass=env:MDREADER_ANDROID_SIGNING_PASSWORD",
        "-p:AndroidSigningKeyPass=env:MDREADER_ANDROID_SIGNING_PASSWORD",
        "-p:ApplicationVersion=$VersionCode", "-p:ApplicationDisplayVersion=$VersionName"
    )
    if ($AndroidSdkDirectory) { $publishArguments += "-p:AndroidSdkDirectory=$AndroidSdkDirectory" }
    if ($JavaSdkDirectory) { $publishArguments += "-p:JavaSdkDirectory=$JavaSdkDirectory" }

    & dotnet @publishArguments
    if ($LASTEXITCODE -ne 0) { throw "A publicacao Android falhou. Consulte os erros acima." }

    $signedPackages = @(Get-ChildItem -LiteralPath $publishPath -Filter "*-Signed.apk" -File)
    if ($signedPackages.Count -ne 1) { throw "Nao foi encontrado um unico APK assinado na pasta de publicacao." }

    New-Item -ItemType Directory -Path $distributionPath -Force | Out-Null
    $finalApkPath = Join-Path $distributionPath "MDReader.apk"
    Copy-Item -LiteralPath $signedPackages[0].FullName -Destination $finalApkPath -Force
    Write-Host ""
    Write-Host "APK assinado gerado em:"
    Write-Host $finalApkPath
} finally {
    $env:MDREADER_ANDROID_SIGNING_PASSWORD = $previousSigningPassword
}
