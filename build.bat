@echo off
echo Building SK Auto Work Tracking System...
echo.

REM Clean previous builds
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"

REM Restore NuGet packages
echo Restoring NuGet packages...
dotnet restore SKAuto.sln

REM Build solution
echo Building solution...
dotnet build SKAuto.sln -c Release -p:Platform="Any CPU"

if %ERRORLEVEL% neq 0 (
    echo Build failed!
    pause
    exit /b 1
)

echo Build successful!
echo.

REM Create deployment folder
if not exist "Deployment" mkdir "Deployment"
if not exist "Deployment\Database" mkdir "Deployment\Database"
if not exist "Deployment\Reports" mkdir "Deployment\Reports"
if not exist "Deployment\Templates" mkdir "Deployment\Templates"

REM Copy files
echo Copying files...
xcopy "Source\SKAuto.UI\bin\Release\net8.0-windows\*.*" "Deployment\" /E /Y
copy "README.md" "Deployment\"
copy "LICENSE" "Deployment\"

REM Create shortcut script
echo Creating shortcuts...
(
echo @echo off
echo echo Starting SK Auto Work Tracking System...
echo "%~dp0SKAuto.UI.exe"
) > "Deployment\Start SKAuto.bat"

echo.
echo Deployment ready in 'Deployment' folder!
pause