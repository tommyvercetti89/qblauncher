[Setup]
AppId={{8F9E7A21-3C45-4A1E-B9C2-71F6D2E49A10}
AppName=QBLauncher Pro
AppVersion=1.0.0
AppPublisher=tommyvercetti89
DefaultDirName={autopf}\QBLauncher Pro
DefaultGroupName=QBLauncher Pro
DisableProgramGroupPage=yes
SetupIconFile=QBLauncher.ico
UninstallDisplayIcon={app}\QBLauncherPro.exe
OutputDir=.
OutputBaseFilename=QBLauncherPro-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Dirs]
Name: "{app}"; Permissions: users-modify

[Files]
Source: "csharp-dotnet\publish\QBLauncherPro.exe"; DestDir: "{app}"; DestName: "QBLauncherPro.exe"; Flags: ignoreversion

[Icons]
Name: "{group}\QBLauncher Pro"; Filename: "{app}\QBLauncherPro.exe"
Name: "{group}\QBLauncher Pro Kaldır"; Filename: "{uninstallexe}"
Name: "{autodesktop}\QBLauncher Pro"; Filename: "{app}\QBLauncherPro.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\QBLauncherPro.exe"; Description: "{cm:LaunchProgram,QBLauncher Pro}"; Flags: nowait postinstall skipifsilent
