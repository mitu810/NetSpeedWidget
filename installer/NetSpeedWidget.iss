#ifndef SourceDirectory
  #error SourceDirectory must be supplied by scripts/publish.ps1
#endif
#ifndef OutputDirectory
  #define OutputDirectory SourceDirectory
#endif
[Setup]
AppId={{8ADC845B-A790-4F50-98EA-8C87FE0309A8}
AppName=NetSpeedWidget
AppVersion=2.0.0
DefaultDirName={localappdata}\Programs\NetSpeedWidget
DefaultGroupName=NetSpeedWidget
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDirectory}
OutputBaseFilename=NetSpeedWidget-Setup
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\NetSpeedWidget.exe
CloseApplications=yes
RestartApplications=no
[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"
[Files]
Source: "{#SourceDirectory}\NetSpeedWidget.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDirectory}\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDirectory}\THIRD_PARTY_NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDirectory}\licenses\*"; DestDir: "{app}\licenses"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "installed.marker"; DestDir: "{app}"; Flags: ignoreversion
[Icons]
Name: "{group}\NetSpeedWidget"; Filename: "{app}\NetSpeedWidget.exe"
Name: "{autodesktop}\NetSpeedWidget"; Filename: "{app}\NetSpeedWidget.exe"; Tasks: desktopicon
[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked
[Run]
Filename: "{app}\NetSpeedWidget.exe"; Description: "启动 NetSpeedWidget"; Flags: nowait postinstall skipifsilent
[UninstallRun]
Filename: "{app}\NetSpeedWidget.exe"; Parameters: "--unregister-hardware"; Flags: runhidden waituntilterminated; RunOnceId: "HardwareTaskCleanup"
[UninstallDelete]
Type: files; Name: "{app}\installed.marker"
[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupValue: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'NetSpeedWidget', StartupValue) then
      if Pos(ExpandConstant('{app}\NetSpeedWidget.exe'), StartupValue) > 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'NetSpeedWidget');
end;

[LangOptions]
LanguageName=中文（简体）
LanguageID=$0804
LanguageCodePage=0
DialogFontName=Microsoft YaHei UI
[Messages]
SetupAppTitle=安装
SetupWindowTitle=安装 - %1
UninstallAppTitle=卸载
UninstallAppFullTitle=卸载 %1
InformationTitle=提示
ConfirmTitle=确认
ErrorTitle=错误
ButtonBack=< 上一步(&B)
ButtonNext=下一步(&N) >
ButtonInstall=安装(&I)
ButtonOK=确定
ButtonCancel=取消
ButtonYes=是(&Y)
ButtonYesToAll=全部是(&A)
ButtonNo=否(&N)
ButtonNoToAll=全部否(&O)
ButtonFinish=完成(&F)
ButtonBrowse=浏览(&B)...
ButtonNewFolder=新建文件夹(&M)
WizardSelectDir=选择安装位置
SelectDirDesc=请选择 %1 的安装位置。
SelectDirLabel3=安装程序将把 %1 安装到下列文件夹。
SelectDirBrowseLabel=点击“下一步”继续；如需选择其他文件夹，请点击“浏览”。
WizardSelectTasks=选择附加任务
SelectTasksDesc=请选择安装时需要执行的附加任务。
SelectTasksLabel2=请选择安装 %1 时需要执行的附加任务，然后点击“下一步”。
WizardReady=准备安装
ReadyLabel1=安装程序已准备好开始安装 %1。
ReadyLabel2a=点击“安装”继续；如需更改设置，请点击“上一步”。
ReadyMemoDir=安装位置：
ReadyMemoTasks=附加任务：
WizardInstalling=正在安装
InstallingLabel=正在安装 %1，请稍候。
StatusExtractFiles=正在复制程序文件...
StatusCreateIcons=正在创建快捷方式...
FinishedHeadingLabel=%1 安装完成
FinishedLabelNoIcons=已完成 %1 的安装。点击“完成”退出安装程序。
FinishedLabel=已完成 %1 的安装。可通过快捷方式运行程序。
WelcomeLabel1=欢迎使用 [name] 安装程序
WelcomeLabel2=这将在您的计算机上安装 [name/ver]。%n%n点击“下一步”继续。
ExitSetupTitle=退出安装
ExitSetupMessage=安装尚未完成，确定退出吗？
ConfirmUninstall=确定卸载 %1 吗？
UninstallStatusLabel=正在卸载 %1，请稍候。
UninstalledAll=已从计算机中卸载 %1。用户配置和日志已保留。
