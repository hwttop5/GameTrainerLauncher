; 游戏修改器启动器 - Inno Setup 安装脚本
; 打包前请先执行: dotnet publish GameTrainerLauncher.UI -p:PublishProfile=FolderProfile

#define MyAppName "游戏修改器启动器"
#define MyAppExeName "GameTrainerLauncher.UI.exe"
#define MyAppVersion "1.0.1"
#define PublishDir "..\Publish"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
; 卸载列表中只显示应用名，不显示 "version x.x.x"
AppVerName={#MyAppName}
; 进一步强制卸载列表标题（DisplayName）只显示应用名
UninstallDisplayName={#MyAppName}
AppPublisher=GameTrainerLauncher
DefaultDirName={autopf}\GameTrainerLauncher
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=GameTrainerLauncher_Setup_{#MyAppVersion}
; SetupIconFile 需标准 .ico（多尺寸），当前 logo.ico 若无效可注释；安装后快捷方式使用主程序 exe 图标
; SetupIconFile=..\GameTrainerLauncher.UI\Assets\logo.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "default"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: unchecked

[Files]
; 发布输出目录下的所有文件
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: dirifempty; Name: "{app}"
