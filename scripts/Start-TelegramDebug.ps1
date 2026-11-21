#Requires -Version 7.0

param(
    [ValidateRange(1024, 65535)]
    [int] $Port = 1443,

    [switch] $DisableCfTls
)

$ErrorActionPreference = 'Stop'
$proxyVersion = 'v1.11.4'
$localPath = Join-Path $PSScriptRoot '..' '.local'
$venvPath = Join-Path $localPath "tgws-$proxyVersion"
$venvPython = Join-Path $venvPath 'Scripts' 'python.exe'
$proxyPath = Join-Path $venvPath 'Scripts' 'tg-ws-proxy.exe'

if (-not (Test-Path -LiteralPath $proxyPath)) {
    uv venv --python 3.12 $venvPath
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to create the Telegram debug environment.'
    }

    $source = "https://github.com/Flowseal/tg-ws-proxy/archive/refs/tags/$proxyVersion.zip"
    uv pip install --python $venvPython "tg-ws-proxy @ $source"
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to install the pinned TG WS Proxy source.'
    }
}

$shortcutPath = [IO.Path]::GetFullPath((Join-Path $localPath "telegram-debug-$PID.url"))
$proxyLinkPattern = "tg://proxy\?server=127\.0\.0\.1&port=$Port&secret=dd[0-9a-f]{32}\b"

$proxyArguments = @('--host', '127.0.0.1', '--port', [string] $Port)
if ($DisableCfTls) {
    $proxyArguments += '--no-secure'
}

try {
    & $proxyPath @proxyArguments 2>&1 | ForEach-Object {
        $line = $_.ToString()
        if ($line -match $proxyLinkPattern) {
            [IO.File]::WriteAllLines($shortcutPath, @('[InternetShortcut]', "URL=$($Matches[0])"))
            Write-Output "Telegram shortcut: $shortcutPath"
        }

        Write-Output ($line -replace '(?i)\b[0-9a-f]{32,}\b', '[redacted]')
    }
    $proxyExitCode = $LASTEXITCODE
} finally {
    [IO.File]::Delete($shortcutPath)
}

exit $proxyExitCode
