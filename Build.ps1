$ErrorActionPreference = 'Stop'

$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    throw 'The Windows .NET Framework C# compiler is required.'
}

$output = Join-Path $PSScriptRoot 'TwitchChatWindow.exe'
$sources = @(
    (Join-Path $PSScriptRoot 'Overlay.cs')
    (Join-Path $PSScriptRoot 'TwitchChat.cs')
)
$manifest = Join-Path $PSScriptRoot 'app.manifest'
$icon = Join-Path $PSScriptRoot 'branding\TwitchChatWindow.ico'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 /warnaserror+ `
    /reference:System.Net.Http.dll "/win32manifest:$manifest" "/win32icon:$icon" "/out:$output" @sources
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Write-Output $output
