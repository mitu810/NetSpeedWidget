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

### v2.1.1 更新交互

去掉软件更新页面和导航：版本/检查按钮常驻设置左下角，检查结果显示在按钮旁。SettingsWindow.Updates.cs 用 ContentDialog 的 PrimaryButtonClick 在原弹窗内完成下载、展示进度并切换为安装按钮；事件同步设置 Cancel=true 防止按钮关闭弹窗，异步请求可取消。--smoke-update 验证新版本弹窗；--smoke-update-current 在隔离实例调用真实检查按钮，验证没有新版本且不弹窗。

`--smoke-update --verify-download-popup` 以隔离实例、默认配置打开真实 Release 的弹窗，通过控件自动化触发真实下载，最多等待 75 秒并记录进度和主按钮状态。这个模式只验证下载及按钮切换，安装分支被阻止；下载在本次实际 EXE 目录的 cache 下，Release ZIP 使用白名单，不收录缓存。

### v2.1.2 软件更新页面

恢复第三项软件更新导航；版本、检查按钮与结果移入 UpdateSettingsPanel。页面没有更新内容或进度控件，SettingsWindow.Updates.cs 的弹窗流程不变。隔离验证先进入更新页面，记录 updatePageVisible、panelWidth 及按钮尺寸，分别检查无更新结果与新版本弹窗。

### v2.1.3 AMD CPU 温度

当前 Ryzen 5 5600GT 在 LibreHardwareMonitorLib 0.9.6 下提供 `Core (Tctl/Tdie)`，普通权限检测为 0 °C，独立管理员检测为 66.6 °C。设置显示“硬件采集已连接”，HTTP CPU 温度仍不可用；原因是旧筛选器只接受 `Core Average`。修复保留原优先级，仅在 AMD CPU 节点按 Tdie、Tctl/Tdie、Tctl 选择正数有效温度，零值仍显示不可用。不使用主板/SuperIO 或 CPU Package 冒充核心温度。测试覆盖 AMD、Intel、零值及来源隔离。管理员检测是独立只读探针，不等同于发布版运行验收。升级主 EXE 不会自动替换 Program Files 中的旧版采集程序；用户需在设置点击“授权/更新采集程序”确认 UAC。HardwareTaskService.Install 在重新注册任务后停掉旧实例，再启动新版；用户升级后需现场确认接口数值。

### v2.1.4 设置窗口首次显示

用户报告偶发首次打开设置窗口时约 5 秒黑屏，只看得到右上角原生窗口按钮；关闭后再打开正常。隔离 `--smoke-dpi` 的设置窗口 XAML 初始化到 `Loaded` 约 0.3 秒，不能复现现场症状。构造期间 `RefreshSystemStatusServerState` 原本同步读取服务状态，运行中的服务会枚举网卡；这是可阻塞 UI 的代码路径，但尚无证据证明它就是现场的 5 秒根因。

状态查询现在通过后台任务执行，使用刷新序号防止旧结果覆盖新设置，并在窗口关闭后停止写回；系统状态页的服务文字可短暂显示“正在读取…”。这符合[微软 WinUI 启动性能建议](https://learn.microsoft.com/en-us/windows/apps/develop/performance/app-startup-performance)中将非首帧必需的慢操作延后执行的做法。设置窗口将 `xaml-initialized`、`window-configured`、`settings-loaded`、`constructor-complete`、`root-loaded`、`first-render` 耗时写入实际 EXE 目录 `app.log`。`first-render` 是 WinUI 渲染事件，不等于屏幕像素已呈现。隔离 `--smoke-dpi --simulate-slow-server-state` 将查询人为延迟 5 秒，验证 `first-render` 仍在约 0.23 秒发生；该模式使用独立实例和默认设置，不能替代重启后真实安装版验收。若升级后仍黑屏，对比同一次打开的各阶段耗时，定位是 XAML 初始化、设置加载，还是布局/渲染阶段；保留当前用户实例，避免强行关闭。
