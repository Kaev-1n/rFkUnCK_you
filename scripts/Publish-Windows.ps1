#Requires -Version 7.0

param(
    [string] $Dotnet = 'dotnet',
    [switch] $UseCachedPythonPackages
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $root 'src' 'Rfkunck.Desktop' 'Rfkunck.Desktop.csproj'
$dotnetExe = (Get-Command $Dotnet -CommandType Application).Source
$buildPath = Join-Path $root '.local' ("windows-build-" + [Guid]::NewGuid().ToString('N'))
$package = Join-Path $buildPath 'rFkUnCK_you'
$dotnetArtifacts = Join-Path $buildPath 'dotnet-artifacts'
$proxy = Join-Path $package 'tg-ws-proxy'
$pythonCache = Join-Path $root '.local' 'flowseal-package'
$pythonArchive = Join-Path $pythonCache 'python.3.12.10.nupkg'
$pythonHash = '0EB85C2DFCCCCF1B17352DE4C397F69194035B7D37149EACC16F1147D93DE3B8'

New-Item -ItemType Directory -Path $buildPath,$pythonCache -Force | Out-Null
if (-not (Test-Path -LiteralPath $pythonArchive) -or
    (Get-FileHash -LiteralPath $pythonArchive -Algorithm SHA256).Hash -ne $pythonHash) {
    $pythonDownload = Join-Path $buildPath 'python.3.12.10.nupkg'
    Invoke-WebRequest -Uri 'https://api.nuget.org/v3-flatcontainer/python/3.12.10/python.3.12.10.nupkg' -OutFile $pythonDownload -TimeoutSec 45
    if ((Get-FileHash -LiteralPath $pythonDownload -Algorithm SHA256).Hash -ne $pythonHash) {
        throw 'The official Python NuGet package did not match the expected SHA-256.'
    }

    Move-Item -LiteralPath $pythonDownload -Destination $pythonArchive -Force
}

& $dotnetExe publish $project -c Release -r win-x64 --self-contained true --artifacts-path $dotnetArtifacts -o $package --nologo
if ($LASTEXITCODE -ne 0) { throw 'Publishing the Windows app failed.' }

Get-ChildItem -LiteralPath $package -File -Filter '*.pdb' | ForEach-Object {
    Remove-Item -LiteralPath $_.FullName
}

New-Item -ItemType Directory -Path $proxy -Force | Out-Null
tar.exe -xf $pythonArchive -C $proxy --strip-components 1 tools
if ($LASTEXITCODE -ne 0) { throw 'Extracting the Python runtime failed.' }
[IO.File]::WriteAllLines((Join-Path $proxy 'python312._pth'), @('.', 'Lib', 'DLLs', 'site-packages'))

$uvArguments = @('pip', 'install', '--no-deps', '--python', '3.12', '--target', (Join-Path $proxy 'site-packages'),
    '--requirement', (Join-Path $PSScriptRoot 'requirements-tgws-win-x64.txt'))
if ($UseCachedPythonPackages) { $uvArguments += '--offline' }
uv @uvArguments
if ($LASTEXITCODE -ne 0) { throw 'Installing the pinned Flowseal dependencies failed.' }

& (Join-Path $proxy 'python.exe') -m proxy.tg_ws_proxy --help | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'The packaged Flowseal CLI did not start.' }

Copy-Item -LiteralPath (Join-Path $root 'LICENSE'),(Join-Path $root 'README.md'),(Join-Path $root 'AGENTS.md') -Destination $package
$licenses = Join-Path $package 'licenses'
$dotnetLicenseDir = Join-Path $licenses 'dotnet'
New-Item -ItemType Directory -Path $dotnetLicenseDir -Force | Out-Null
$dotnetRoot = Split-Path -Parent $dotnetExe
Copy-Item -LiteralPath (Join-Path $dotnetRoot 'LICENSE.txt'),(Join-Path $dotnetRoot 'ThirdPartyNotices.txt') -Destination $dotnetLicenseDir

$assemblyFolder = Join-Path $dotnetArtifacts 'obj' 'Rfkunck.Desktop'
$restoreAssets = Get-Content -LiteralPath (Join-Path $assemblyFolder 'project.assets.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($dependency in $restoreAssets.libraries.Values) {
    if ($dependency.type -ne 'package') { continue }
    $licenseFiles = @($dependency.files | Where-Object { (Split-Path -Leaf $_) -match '^(LICENSE|NOTICE|COPYING|THIRD.?PARTY.?NOTICES)' })
    if ($licenseFiles.Count -eq 0) { continue }
    $packageCache = $restoreAssets.packageFolders.Keys | Where-Object { Test-Path -LiteralPath (Join-Path $_ $dependency.path) } | Select-Object -First 1
    $packageLicenses = Join-Path $licenses 'nuget' $dependency.path
    New-Item -ItemType Directory -Path $packageLicenses -Force | Out-Null
    foreach ($licenseFile in $licenseFiles) {
        Copy-Item -LiteralPath (Join-Path $packageCache $dependency.path $licenseFile) -Destination $packageLicenses
    }
}

$artifactPath = Join-Path $root 'artifacts'
New-Item -ItemType Directory -Path $artifactPath -Force | Out-Null
$version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version
$archive = Join-Path $artifactPath "rFkUnCK_you-$version-win-x64.zip"
Compress-Archive -LiteralPath $package -DestinationPath $archive -Force
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Select-Object Path,Hash
