# 软件更新与 Release 内容规范

## 用户流程

在设置 → 软件更新点击“检查更新”。正常启动后会在后台检查一次，发现新版本弹窗显示内容；也可手动检查。只查询公开稳定版本，不发送配置、不需要 GitHub 令牌，不自动下载或安装。按数字比较版本，发现更高版本时显示 Release 完整说明、日期、包大小。

新版本弹窗提供“下载更新”和“稍后”。下载时设置页显示进度条，可取消；完成后再次弹窗，可安装或稍后安装。重新检查/重启后按长度和 SHA-256 验证缓存，通过时直接显示“安装更新”，不重复下载。只有点击安装并准备就绪后才退出软件。网络、限流、资产缺失、校验失败均保留运行中的软件。安装版与便携版根据程序目录里的 installed.marker 区分，不自动切换发行类型。

- 安装版：下载同版本 Setup.exe，普通权限打开安装向导，默认原安装目录。请不要更改安装位置。完成或取消后重新启动原路径程序。更新向导不重复启动程序，由更新辅助程序统一负责重启。
- 便携版：下载 Portable.zip，校验和解压完成后等待原进程退出。只替换主程序、使用说明和许可证，不覆盖 settings.json、日志、用户文件。显示独立安装进度条；替换失败尝试恢复旧文件。符号链接、目录联接、目录不可写时拒绝更新，请手动处理。
- 备份和下载保留在实际 EXE 目录 cache/updates 下；下载按版本和发行类型保存，安装任务在 jobs 下使用独立随机目录。失败记录 error.txt 和备份路径；软件更新页显示上次结果。可以在成功验证新版后手动清理旧下载与备份。
- 开发构建可检查版本；未嵌入更新程序时不执行安装。正式安装/便携包都嵌入自包含的普通权限更新程序，用户不需要 .NET SDK。

## 实现与安全边界

UpdateService 查询 GitHub REST releases/latest，按本仓库固定的资产 URL/名称选取 Windows x64 文件。优先使用 GitHub 资产 digest，旧资产回退 SHA256SUMS.txt；没有 SHA-256 时拒绝更新。限制包大小 512 MB、元数据请求 30 秒、下载 15 分钟；流式下载校验字节数及哈希。

UpdateLauncher 提取独立更新程序，传递进程 ID/启动时间、实际 EXE 目录和已校验包。辅助程序再次校验，便携包检查程序版本与 Release 一致，校验路径白名单、重复路径和解压大小。收到准备信号后主程序按正常退出流程释放托盘、采集与 HTTP 资源。辅助程序最多等待原进程 60 秒，不强杀原进程。

SHA-256 检测文件损坏或附件不一致，信任来自 GitHub HTTPS 与仓库权限；目前发行程序未做代码签名，不应把哈希描述为发行者数字签名。硬件授权任务与更新逻辑分离，更新不弹出硬件 UAC，不修改授权策略。

## Release 固定结构

第一行 `# NetSpeedWidget v主版本.次版本.修订号`，随后按以下顺序写二级标题，每节至少一条 `- 内容`，没有对应改动写 `- 无。`：

1. 新增功能
2. 优化改进
3. 问题修复
4. 更新说明（兼容性、用户需要做什么、已知边界）
5. 下载文件（当前版本 Setup.exe、Portable.zip、SHA256SUMS.txt）
6. 验证情况（明确自动化、隔离环境、真实现场尚未验收的边界）

更新页按纯文本展示 Markdown 原文，保留列表/标题内容，不执行 HTML 或脚本。

发布前执行 `python scripts/validate-release.py 发布说明文件.md v2.1.0`。脚本检查标题顺序、非空列表、版本标题与下载文件匹配；通过后将同一正文传给 GitHub Release。版本唯一来源为主 csproj 的 Version，publish.ps1 自动传给 Inno Setup，不能手动给安装版另写一个版本。

## 验证流程

运行 NetSpeedWidget.Tests：覆盖版本 2.10 > 2.9、相同版本、发行资产缺失、限流、哈希错误、取消、ZIP 路径穿越/重复文件/配置拒绝、文件占用回滚及用户配置保留。真实安装向导取消/完成、权限受限目录、跨机器网络和软件重启仍需分别验收，不能用单元测试替代。

官方参考：[GitHub Release API](https://docs.github.com/en/rest/releases/releases)、[Release 资产 API](https://docs.github.com/en/rest/releases/assets)、[Inno Setup 命令参数](https://jrsoftware.org/ishelp/topic_setupcmdline.htm)。

## 数据位置调整

安装版与便携版都在实际 EXE 目录保存 settings.json、app.log、update-result.txt 和 cache（下载包、安装备份、硬件授权前解压缓存）。升级旧安装版时，仅在新位置没有 settings.json 的情况下复制 `%LOCALAPPDATA%/NetSpeedWidget/settings.json`，保留原件，不覆盖新配置。目录必须可写；默认当前用户安装目录满足此要求。管理员硬件采集的受保护运行载荷仍保留在 Program Files，这是提权安全边界；.NET/Windows 运行时自身的临时解压位置由运行时管理，不是应用更新缓存。

## 本次本机验收

正式自包含 v2.1.0 的资源检查通过（普通权限、硬件资源、更新 EXE）。200% DPI 的实际设置窗口宽 640 逻辑像素，更新页可见，新版本 ContentDialog 弹窗已打开并随隔离实例退出。正式辅助程序在隔离目录执行 v2.0.0 → v2.1.0 便携替换，主程序版本与 Release 一致、配置哈希不变、备份存在；未停止用户原运行实例。真实安装向导完整升级/取消仍需现场验收。

新增进度和弹窗控件时，发现原 App.xaml 未合并 XamlControlsResources，实际正式 EXE 曾报 TabViewButtonBackground 缺失；已按微软资源字典建议补齐并重测。参考 [微软资源字典说明](https://learn.microsoft.com/zh-cn/windows/apps/develop/platform/xaml/xaml-resource-dictionary)、[ContentDialog 要求](https://learn.microsoft.com/zh-cn/windows/apps/develop/ui/controls/dialogs-and-flyouts/dialogs)。
