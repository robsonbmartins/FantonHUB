[Setup]
AppName=FantonHUB - MIDI Router
AppVersion=1.0.0
DefaultDirName={autopf}\CmcMidiRouter
DefaultGroupName=FantonHUB - MIDI Router
OutputDir=Output
OutputBaseFilename=CmcMidiRouter_Setup
SetupIconFile=app.ico
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin

[Files]
Source: "bin\Release\net6.0-windows\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "app.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\FantonHUB - MIDI Router"; Filename: "{app}\CmcMidiRouter.exe"; IconFilename: "{app}\app.ico"
Name: "{commondesktop}\FantonHUB - MIDI Router"; Filename: "{app}\CmcMidiRouter.exe"; IconFilename: "{app}\app.ico"

[Run]
Filename: "{app}\CmcMidiRouter.exe"; Description: "Launch FantonHUB - MIDI Router"; Flags: nowait postinstall skipifsilent
