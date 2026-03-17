; WindowsHotSpot Installer Script
; Requires: dotnet publish output in WindowsHotSpot\bin\Release\net10.0-windows\win-x64\publish\

[Setup]
AppId={{F3A2B1C4-8E5D-4F9A-B2C7-1D3E6F8A0B5C}
AppName=WindowsHotSpot
AppVersion=1.0.0
AppPublisher=WindowsHotSpot
DefaultDirName={localappdata}\WindowsHotSpot
DefaultGroupName=WindowsHotSpot
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=WindowsHotSpot-Setup
Compression=lzma2
SolidCompression=yes
SetupIconFile=..\WindowsHotSpot\Resources\app.ico
UninstallDisplayIcon={app}\WindowsHotSpot.exe
DisableProgramGroupPage=yes
ArchitecturesInstallIn64BitMode=x64compatible
RestartApplications=yes

[Files]
Source: "..\WindowsHotSpot\bin\Release\net10.0-windows\win-x64\publish\WindowsHotSpot.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userstartmenu}\WindowsHotSpot"; Filename: "{app}\WindowsHotSpot.exe"
Name: "{userdesktop}\WindowsHotSpot"; Filename: "{app}\WindowsHotSpot.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM WindowsHotSpot.exe /T', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;

[UninstallDelete]
Type: files; Name: "{app}\settings.json"
