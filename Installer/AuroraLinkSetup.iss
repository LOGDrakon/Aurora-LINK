; ============================================================
; Aurora-LINK Installer Script — Inno Setup
; ============================================================
; Pré-requis : publier l'application avant de compiler ce script
;   dotnet publish Aurora-LINK\Aurora-LINK\Aurora-LINK.csproj -c Release -r win-x64 --self-contained true -o .\publish
;
; Puis ouvrir ce fichier dans Inno Setup Compiler et cliquer Build.
; ============================================================

#define MyAppName      "Aurora-LINK"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "LOGDrakon"
#define MyAppExeName   "Aurora-LINK.exe"

[Setup]
AppId={{A3F1B2C4-D5E6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\InstallerOutput
OutputBaseFilename=AuroraLinkSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=admin
MinVersion=10.0.17763

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Copier tout le contenu du dossier publish
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Désinstaller {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer {#MyAppName}"; Flags: nowait postinstall skipifsilent
