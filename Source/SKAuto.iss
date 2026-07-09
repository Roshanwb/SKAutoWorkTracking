#define MyAppName "SKAuto Work Tracking"
#define MyAppVersion "2.0.0.0"
#define MyAppPublisher "Insights"
#define MyAppExeName "SKAuto.UI.exe"

[Setup]
AppId={{8A5C1B2C-4C5E-4C2A-BF6A-8A7C8B9C0D1E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=.\Installer
OutputBaseFilename=SKAutoWorkTracking_Setup
SetupIconFile=.\SKAuto.UI\logo.ico

[Files]
Source: "E:\Non Theocratic\SK Auto\Program\SKAutoWorkTracking\Source\SKAuto.UI\bin\Release\net8.0-windows\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "E:\Non Theocratic\SK Auto\Program\SKAutoWorkTracking\Source\SKAuto.UI\bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"
Name: "startup"; Description: "Run {#MyAppName} at Windows startup"; GroupDescription: "Startup options:"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: postinstall nowait skipifsilent