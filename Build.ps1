$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Source') -Filter '*.cs' -File | ForEach-Object FullName)
$outputPath = Join-Path $PSScriptRoot 'SonarEchoGuard.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /warn:4 "/out:$outputPath" /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'SonarEchoGuard.exe') -Algorithm SHA256
