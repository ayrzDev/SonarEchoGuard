$ErrorActionPreference = 'Stop'
$installRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\SonarEchoGuard'))
$programRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
if ((Split-Path -Parent $installRoot) -ne $programRoot -or (Split-Path -Leaf $installRoot) -ne 'SonarEchoGuard') { throw 'Unexpected uninstall target.' }
$exe = Join-Path $installRoot 'SonarEchoGuard.exe'
if (Test-Path -LiteralPath $exe) {
    $stopProcess = Start-Process -FilePath $exe -ArgumentList '--stop' -WindowStyle Hidden -Wait -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    do {
        $running = @(Get-Process -Name 'SonarEchoGuard' -ErrorAction SilentlyContinue | Where-Object Path -eq $exe)
        if ($running.Count -eq 0) { break }
        Start-Sleep -Milliseconds 250
    } while([DateTime]::UtcNow -lt $deadline)
    if ($running.Count -gt 0) { throw 'Exit the app through its tray menu before uninstalling. It was not force-terminated.' }
}
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$startup = (Get-ItemProperty -LiteralPath $runKey -Name 'SonarEchoGuard' -ErrorAction SilentlyContinue).SonarEchoGuard
if ($startup -in @(('"'+$exe+'"'),('"'+$exe+'" --background'))) { Remove-ItemProperty -LiteralPath $runKey -Name 'SonarEchoGuard' }
foreach($name in @('SonarEchoGuard.exe','LICENSE','Read Me.txt')) {
    $filePath = [IO.Path]::GetFullPath((Join-Path $installRoot $name))
    if ((Split-Path -Parent $filePath) -ne $installRoot) { throw 'Unexpected file path.' }
    if (Test-Path -LiteralPath $filePath) { Remove-Item -LiteralPath $filePath -Force }
}
if ((Test-Path -LiteralPath $installRoot) -and @(Get-ChildItem -LiteralPath $installRoot -Force).Count -eq 0) { Remove-Item -LiteralPath $installRoot }
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Sonar Echo Guard.lnk'
if (Test-Path -LiteralPath $shortcutPath) {
    $shell=New-Object -ComObject WScript.Shell
    if ($shell.CreateShortcut($shortcutPath).TargetPath -eq $exe) { Remove-Item -LiteralPath $shortcutPath }
}
Write-Output 'Sonar Echo Guard uninstalled. Diagnostic logs and this source package were kept.'
