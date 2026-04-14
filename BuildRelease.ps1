param(
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $repoRoot 'src\UnderSuperWax\UnderSuperWax.csproj'
$packageDir = Join-Path $repoRoot 'package'
$releaseDir = Join-Path $repoRoot 'releases'
$stagingDir = Join-Path $env:TEMP ('UnderSuperWax-' + [Guid]::NewGuid().ToString('N'))
$version = '0.0.1-alpha'
$valheimRoot = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim'
$jotunnRoot = Join-Path $env:APPDATA 'r2modmanPlus-local\Valheim\cache\ValheimModding-Jotunn\2.29.0\plugins'
$zipName = "UnderSuperWax-$version.zip"
$zipPath = Join-Path $releaseDir $zipName

$requiredFiles = @(
    (Join-Path $valheimRoot 'BepInEx\core\BepInEx.dll'),
    (Join-Path $valheimRoot 'BepInEx\core\0Harmony.dll'),
    (Join-Path $jotunnRoot 'Jotunn.dll'),
    (Join-Path $valheimRoot 'valheim_Data\Managed\Assembly-CSharp.dll'),
    (Join-Path $valheimRoot 'valheim_Data\Managed\assembly_valheim.dll'),
    (Join-Path $valheimRoot 'valheim_Data\Managed\UnityEngine.dll'),
    (Join-Path $valheimRoot 'valheim_Data\Managed\UnityEngine.InputLegacyModule.dll')
)

foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path $requiredFile)) {
        throw "Missing required reference: $requiredFile"
    }
}

if (Test-Path $stagingDir) {
    Remove-Item $stagingDir -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

dotnet build $projectPath -c $Configuration -p:ValheimRoot="$valheimRoot" -p:JotunnRoot="$jotunnRoot"
if ($LASTEXITCODE -ne 0) {
    throw "Build failed."
}

$filesToCopy = @('manifest.json', 'README.md', 'description.md', 'icon.png')
foreach ($fileName in $filesToCopy) {
    Copy-Item (Join-Path $packageDir $fileName) $stagingDir -Force
}

$pluginsDir = Join-Path $stagingDir 'plugins'
New-Item -ItemType Directory -Path $pluginsDir -Force | Out-Null

$builtDll = Join-Path $repoRoot 'src\UnderSuperWax\bin\Release\netstandard2.1\UnderSuperWax.dll'
if (-not (Test-Path $builtDll)) {
    throw "Built plugin DLL not found at $builtDll"
}

Copy-Item $builtDll $pluginsDir -Force

Compress-Archive -Path (Join-Path $stagingDir '*') -DestinationPath $zipPath -Force
Remove-Item $stagingDir -Recurse -Force
Write-Host "Created $zipPath"
