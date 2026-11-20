$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'SonarEchoGuard.exe'
if (-not (Test-Path -LiteralPath $exe)) { & (Join-Path $PSScriptRoot 'Build.ps1') }
$resultFile = Join-Path $PSScriptRoot 'Test Results.json'
$process = Start-Process -FilePath $exe -ArgumentList @('--self-test',('"'+$resultFile+'"')) -WindowStyle Hidden -Wait -PassThru
if (-not (Test-Path -LiteralPath $resultFile)) { throw 'The test runner did not produce a report.' }
$result = Get-Content -LiteralPath $resultFile -Raw -Encoding utf8 | ConvertFrom-Json
if ($process.ExitCode -ne 0 -or -not $result.Passed) { throw ('Audio integration tests failed: '+$result.Error) }
$result.Checks | ForEach-Object { Write-Output ('PASS: '+$_) }
