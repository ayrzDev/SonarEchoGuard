$ErrorActionPreference = 'Stop'
$installRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\SonarEchoGuard'))
$programRoot = [IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs'))
if ((Split-Path -Parent $installRoot) -ne $programRoot) { throw 'Unexpected installation target.' }
if (Test-Path -LiteralPath $installRoot) { throw 'Installation directory already exists; inspect it before replacing an installation.' }
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$oldStartup = Get-ItemProperty -LiteralPath $runKey -Name 'SonarEchoGuard' -ErrorAction SilentlyContinue
if ($oldStartup) { throw 'A startup entry already exists; it was not overwritten.' }
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
foreach($name in @('SonarEchoGuard.exe','LICENSE','Read Me.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $installRoot $name) -ErrorAction Stop
}
$installedExe = Join-Path $installRoot 'SonarEchoGuard.exe'
if ((Get-FileHash -LiteralPath $installedExe).Hash -ne (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'SonarEchoGuard.exe')).Hash) { throw 'Installed executable hash mismatch.' }
if (-not (Test-Path -LiteralPath $runKey)) { New-Item -Path $runKey -Force | Out-Null }
New-ItemProperty -LiteralPath $runKey -Name 'SonarEchoGuard' -PropertyType String -Value ('"'+$installedExe+'" --background') -ErrorAction Stop | Out-Null
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Programs')) 'Sonar Echo Guard.lnk'
if ((Test-Path -LiteralPath (Split-Path -Parent $shortcutPath)) -and -not (Test-Path -LiteralPath $shortcutPath)) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath); $shortcut.TargetPath=$installedExe; $shortcut.Arguments='--show'; $shortcut.WorkingDirectory=$installRoot; $shortcut.Save()
}
$guardProcess = Start-Process -FilePath $installedExe -WindowStyle Hidden -PassThru
[ordered]@{installed_executable=$installedExe;sha256=(Get-FileHash -LiteralPath $installedExe).Hash;startup_enabled=$true;process_id=$guardProcess.Id;shortcut=$shortcutPath} | ConvertTo-Json
