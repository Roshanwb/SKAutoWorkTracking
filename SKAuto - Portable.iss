#define MyAppName "SKAuto Work Tracking"
#define MyAppVersion "1.0.0.0"
#define MyAppPublisher "SK Auto"
#define MyAppExeName "SKAuto.UI.exe"

[Setup]
AppId={{8A5C1B2C-4C5E-4C2A-BF6A-8A7C8B9C0D1E}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={userappdata}\{#MyAppName}          ; user folder, no admin needed
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir=.\Installer
OutputBaseFilename=SKAutoWorkTracking_Setup
SetupIconFile=.\Source\SKAuto.UI\logo.ico
PrivilegesRequired=lowest     

[Files]
; Point to the self‑contained single file from the publish folder
Source: ".\publish\portable\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Copy logo.png if needed (PDF generator looks for it in the same folder)
Source: ".\publish\portable\logo.png"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: postinstall nowait skipifsilent


[Files]
; Adjust these paths to match your actual publish output
Source: ".\SKAuto.UI\bin\Release\net8.0-windows\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: ".\SKAuto.UI\bin\Release\net8.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: postinstall nowait skipifsilent