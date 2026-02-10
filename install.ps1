# SK Auto Work Tracking System Installer
param(
    [string]$InstallPath = "$env:USERPROFILE\Desktop\SKAuto"
)

Write-Host "SK Auto Work Tracking System Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Check if .NET 8 is installed
$netCheck = Get-ChildItem "HKLM:SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\" | Get-ItemPropertyValue -Name Release
if ($netCheck -lt 528040) {
    Write-Host "Error: .NET 8 Desktop Runtime is required" -ForegroundColor Red
    Write-Host "Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    pause
    exit 1
}

# Create installation directory
Write-Host "Creating installation directory: $InstallPath" -ForegroundColor Green
New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null

# Copy files (assuming we're running from deployment folder)
$sourcePath = ".\*"
Write-Host "Copying application files..." -ForegroundColor Green
Copy-Item -Path $sourcePath -Destination $InstallPath -Recurse -Force

# Create desktop shortcut
$shortcutPath = "$env:USERPROFILE\Desktop\SK Auto.lnk"
$targetPath = "$InstallPath\SKAuto.UI.exe"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $targetPath
$shortcut.WorkingDirectory = $InstallPath
$shortcut.Description = "SK Auto Work Tracking System"
$shortcut.Save()

# Create start menu shortcut
$startMenuPath = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\SK Auto"
New-Item -ItemType Directory -Path $startMenuPath -Force | Out-Null
$startMenuShortcut = "$startMenuPath\SK Auto.lnk"
$shortcut2 = $shell.CreateShortcut($startMenuShortcut)
$shortcut2.TargetPath = $targetPath
$shortcut2.WorkingDirectory = $InstallPath
$shortcut2.Description = "SK Auto Work Tracking System"
$shortcut2.Save()

# Create uninstaller
$uninstallScript = @"
@echo off
echo Uninstalling SK Auto Work Tracking System...
echo.

REM Remove shortcuts
del "%USERPROFILE%\Desktop\SK Auto.lnk"
rmdir /s /q "%APPDATA%\Microsoft\Windows\Start Menu\Programs\SK Auto"

REM Remove application data
echo Removing application data...
rmdir /s /q "%APPDATA%\SKAuto"

REM Remove installation directory
echo Removing installation directory...
rmdir /s /q "%~dp0"

echo.
echo Uninstallation complete!
pause
"@

$uninstallPath = "$InstallPath\Uninstall.bat"
$uninstallScript | Out-File -FilePath $uninstallPath -Encoding ASCII

# Create README
$readme = @"
SK Auto Work Tracking System
============================

Installation Location: $InstallPath

Quick Start:
1. Run 'Start SKAuto.bat' or double-click 'SK Auto' shortcut on desktop
2. On first run, the system will create the database
3. Import PSA plans or create work orders manually

Data Locations:
- Database: %APPDATA%\SKAuto\SKAuto.db
- Backups: Documents\SKAuto\Backups
- Reports: Documents\SKAuto\Reports

Support:
For issues or questions, contact your system administrator.

Version: 1.0.0
"@

$readmePath = "$InstallPath\README.txt"
$readme | Out-File -FilePath $readmePath -Encoding ASCII

Write-Host "`nInstallation complete!" -ForegroundColor Green
Write-Host "Shortcuts created on desktop and start menu" -ForegroundColor Yellow
Write-Host "`nPress any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')