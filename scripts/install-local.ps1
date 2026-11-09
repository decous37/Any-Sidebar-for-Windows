$ErrorActionPreference = 'Stop'
$installRoot = Join-Path $env:LOCALAPPDATA 'Programs\AnySidebarForWindows'
$sourceRoot = $PSScriptRoot
if (Get-Process -Name AnySidebarWindows -ErrorAction SilentlyContinue) { throw '请先从托盘退出 Any Sidebar，再运行安装。' }
if ([IO.Path]::GetFullPath($sourceRoot).TrimEnd('\') -eq [IO.Path]::GetFullPath($installRoot).TrimEnd('\')) { throw '当前已在安装目录，无需再次安装。' }
$stagingRoot = "$installRoot-staging"
$previousRoot = "$installRoot-previous"
if (Test-Path $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null
Get-ChildItem -LiteralPath $sourceRoot | Copy-Item -Destination $stagingRoot -Recurse -Force
if (-not (Test-Path (Join-Path $stagingRoot 'AnySidebarWindows.exe'))) { throw '应用文件不完整，请重新解压下载包。' }
if (Test-Path $previousRoot) { Remove-Item -LiteralPath $previousRoot -Recurse -Force }
if (Test-Path $installRoot) { Move-Item -LiteralPath $installRoot -Destination $previousRoot }
try { Move-Item -LiteralPath $stagingRoot -Destination $installRoot } catch { if (Test-Path $previousRoot) { Move-Item -LiteralPath $previousRoot -Destination $installRoot }; throw }
$shortcutRoot = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
$shortcut = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $shortcutRoot 'Any Sidebar for Windows.lnk'))
$shortcut.TargetPath = Join-Path $installRoot 'AnySidebarWindows.exe'
$shortcut.WorkingDirectory = $installRoot
$shortcut.Save()
if (Test-Path $previousRoot) { Remove-Item -LiteralPath $previousRoot -Recurse -Force }
Write-Host '安装完成。API Key、会话和偏好已保留。可从开始菜单启动 Any Sidebar。'
