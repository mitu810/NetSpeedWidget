# NetSpeedWidget

Windows 桌面实时网速小组件，使用 WinUI 3 / .NET 8，支持悬浮窗、多屏任务栏和可选的系统状态网页。原创代码采用 MIT 许可证。

## 下载

从 [最新 Release](https://github.com/mitu810/NetSpeedWidget/releases/latest) 下载：

- **安装版**：`NetSpeedWidget-v2.1.4-win-x64-Setup.exe`，按向导安装后从开始菜单运行。
- **便携版**：`NetSpeedWidget-v2.1.4-win-x64-Portable.zip`，解压到固定、可写的目录，双击 `NetSpeedWidget.exe`。
- **校验**：`SHA256SUMS.txt`，用于检查下载文件完整性。

支持 Windows 10 2004（19041）及以上、Windows 11，当前提供 x64。用户无需安装 Visual Studio、.NET SDK 或 Windows App SDK；发行包包含运行依赖，首次启动会自动解压依赖。

## 功能

- 上传、下载实时速率，悬浮或任务栏左/右显示。
- 多显示器任务栏窗口独立维护，支持目标屏幕 DPI 缩放。
- 深浅主题、字体大小、置顶、位置锁定和当前用户登录自启。
- 可选 CPU/GPU/内存等状态及 IPv4/IPv6 双栈网页仪表盘。
- 可选访问密码；中文密码、采集快照缓存和有界 HTTP 请求处理。
- 在设置的“软件更新”页面检查新版本，有更新时弹窗展示内容，并在同一弹窗下载、安装。

## 使用

右键托盘图标打开设置。在“网速显示”选择展示模式；Windows 本身需启用所有显示器显示任务栏，新屏幕才有可挂载任务栏。

“开机启动”表示当前用户登录 Windows 后启动。软件主进程使用普通权限，Windows 启动应用设置里的禁用仍然有效。便携目录移动后应重新检查启动开关。

网速显示无需管理员权限。需要温度时，开启系统状态并点击“授权硬件采集”，完成一次 UAC；升级温度采集逻辑后也需再次点击“授权/更新采集程序”确认 UAC；辅助进程提供受权限限制的温度，主界面继续以普通权限运行。当前授权要求同一登录用户具有可提升的管理员权限，不支持标准账户借用另一管理员账户的凭据。取消授权、传感器/驱动不支持或 CPU 温度为异常零值时显示“不可用”，不影响网速。

配置、日志和 cache 均位于实际 EXE 目录，安装版和便携版一致；目录必须可写。旧安装版升级时在没有新配置的情况下复制原 `%LOCALAPPDATA%\NetSpeedWidget` 中的 settings.json，保留原件。便携转安装可退出后手动复制 settings.json。卸载保留配置；受保护硬件文件暂不自动删除，任务清理失败会记日志。

系统状态 HTTP 默认关闭。开启后未设置密码会允许可访问端口的设备读取指标；该服务不提供 TLS，不应直接暴露到互联网。请查看 [安全说明](https://github.com/mitu810/NetSpeedWidget/blob/main/SECURITY.md)。

## 开发

```powershell
dotnet run --project .\NetSpeedWidget.Tests\NetSpeedWidget.Tests.csproj
dotnet build .\NetSpeedWidget.csproj -p:Platform=x64
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

构建需要 Windows、.NET 8 SDK 和 Windows/WinUI 构建工具；安装包编译需要 Inno Setup 6。完整结构和打包流程见 [开发说明](https://github.com/mitu810/NetSpeedWidget/blob/main/docs/DEVELOPMENT.md)、[发布说明](https://github.com/mitu810/NetSpeedWidget/blob/main/docs/RELEASE.md)。接口见 [系统状态文档](https://github.com/mitu810/NetSpeedWidget/blob/main/docs/接口文档-系统状态.md) 与 [OpenAPI](https://github.com/mitu810/NetSpeedWidget/blob/main/docs/openapi-系统状态.yaml)。

## 验证和已知边界

已通过自动化测试、正式 EXE 资源验证、本机 200% DPI 的首次悬浮/设置窗口布局检查和任务栏挂载检查。注销登录、新虚拟屏幕、实际跨不同 DPI 屏幕、硬件授权链路及安装卸载仍需在不同机器验收。任务栏挂载依赖 Explorer 窗口结构，系统升级或第三方任务栏可能影响兼容性。

启动时自动检查，也可在设置的“软件更新”页面点击“检查更新”。没有新版本时只在按钮旁显示结果；有新版本弹窗显示完整内容，下载进度也在弹窗内，完成后原按钮直接切换为“安装更新”。重新检查直接复用通过校验的缓存。点击“安装更新”后才退出软件：安装版打开原目录安装向导，便携版显示独立安装进度并保留配置；随后重新启动。旧版 v2.0.0 首次升级需手动下载 v2.1.4。更多流程和失败恢复见 [更新说明](https://github.com/mitu810/NetSpeedWidget/blob/main/docs/UPDATES.md)。当前发行 EXE 未做代码签名。

## 许可证

原创代码为 [MIT](https://github.com/mitu810/NetSpeedWidget/blob/main/LICENSE)，第三方组件遵守各自许可，见 [第三方声明](https://github.com/mitu810/NetSpeedWidget/blob/main/THIRD_PARTY_NOTICES.md) 和随附 licenses 目录。
