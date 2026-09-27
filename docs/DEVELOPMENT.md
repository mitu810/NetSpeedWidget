# 开发说明

## 结构

| 项目/目录 | 职责 |
| --- | --- |
| NetSpeedWidget.csproj | WinUI 3 主界面、网速、任务栏、普通权限 HTTP |
| NetSpeedWidget.Hardware | 独立管理员采集、首次授权、交互登录任务 |
| NetSpeedWidget.Tests | 控制台行为测试与必要源码约束 |
| Services | DPI、窗口协调、IPC、HTTP、设置及采集服务 |
| Models | 设置与指标模型 |
| Assets/Dashboard.html | 内嵌网页仪表盘 |
| installer | Inno Setup 当前用户安装脚本 |
| scripts/publish.ps1 | 自包含单文件和安装程序构建 |

## 构建

需要 Windows x64、.NET 8 SDK、Windows SDK/WinUI 构建工具，建议 Visual Studio 安装 Windows 应用开发工作负载。global.json 允许同 feature band/后续 feature band 的 .NET 8 SDK。

```powershell
dotnet run --project .\NetSpeedWidget.Tests\NetSpeedWidget.Tests.csproj
dotnet build .\NetSpeedWidget.csproj -p:Platform=x64
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

安装程序还需要 Inno Setup 6；可通过 `-InstallerCompiler <ISCC.exe路径>` 指定编译器。脚本会生成新的 publish 时间戳目录，不覆盖已有发布。

主应用没有 MSIX 包身份；这不影响普通 EXE 与传统安装程序分发。正式发布内嵌 .NET、Windows App SDK 及自包含硬件包，首次启动会解压运行依赖。仅构建主项目 Debug 不会内嵌硬件包；开发授权需提供 EXE 旁 Hardware 目录，或使用正式发布脚本。

## 设计约束

- XAML 尺寸是逻辑像素，AppWindow/Win32 是物理像素，必须按目标窗口 DPI 转换；字体由 XAML 自动缩放，不重复乘比例。
- 任务栏窗口按 HWND 对应；屏幕增删仅创建/释放变化项。Explorer 挂载仍依赖窗口结构，需跨 Windows 版本验收。
- 主进程使用 asInvoker，管理员采集在独立进程运行。首次任务不存在是正常未授权状态，拒绝 UAC 或采集失败不能关闭主界面。
- HTTP 正文使用 UTF-8 字节读取，16 KB 请求头、64 KB 正文、10 秒截止时间、16 个活动连接；状态串行采集并缓存 1 秒。
- 设置文件原子替换并按修改字段合并；新密码使用带盐 PBKDF2-SHA256，兼容旧 SHA256；日志有限滚动。

接口见 [系统状态接口](接口文档-系统状态.md) 和 [OpenAPI](openapi-系统状态.yaml)。公开源码使用 MIT；第三方组件适用各自许可证，见根目录 THIRD_PARTY_NOTICES.md 与 licenses。

## 验证边界

自动化测试覆盖 HTTP/IPC 分段与大小边界、中文密码、真实 TCP 登录、窗口注册表增删、采集缓存、配置合并和首次授权失败。实际 200% DPI 的新进程窗口布局及任务栏挂载已检查；注销登录、新虚拟屏幕、混合 DPI、管理员硬件链路和完整安装卸载仍需现场验证。不要把纯计算测试或构建成功当作这些场景的验收。

## 软件更新（v2.1.0）

更新页代码在 SettingsWindow.Updates.cs；UpdateService 负责匿名 GitHub 检查和流式下载，UpdateLauncher 负责更新辅助程序就绪握手，NetSpeedWidget.Updater 负责等待原进程、安装向导或便携替换与重启；PortableUpdate 提供白名单解压和备份回滚。publish.ps1 同时嵌入硬件和更新辅助程序。规范、测试与未验收边界见 [UPDATES.md](UPDATES.md)。`--smoke-update` 使用独立实例和默认配置，仅展示更新页布局，五秒后写入 DPI 校验记录并退出。

补充：发现更新统一通过 ContentDialog 弹窗提示；启动后台检查与手动检查复用同一弹窗。UpdateCache 按版本/发行类型管理实际 EXE 下的 cache/updates，下载与安装分开，缓存需重复校验后才能安装。便携安装进度由独立普通权限 WinForms 窗口显示，安装版使用 Inno 的实际安装进度。AppPaths 统一 EXE 目录数据位置，旧安装版配置只在目标不存在时复制迁移。
