# 第三方组件声明

项目原创代码采用 MIT。下列第三方组件及其版权、许可证保持独立；项目 MIT 许可不覆盖第三方组件。licenses 目录保存上游许可和版权通知原文，两种发行版均附带该目录。

| 组件 | 版本 | 许可与来源 |
| --- | --- | --- |
| LibreHardwareMonitorLib | 0.9.6 | MPL-2.0；https://github.com/LibreHardwareMonitor/LibreHardwareMonitor |
| BlackSharp.Core | 1.0.7 | MPL-2.0；https://github.com/Blacktempel/BlackSharp |
| DiskInfoToolkit | 1.1.2 | MPL-2.0；https://github.com/Blacktempel/DiskInfoToolkit |
| RAMSPDToolkit-NDD | 1.4.2 | MPL-2.0；https://github.com/Blacktempel/RAMSPDToolkit |
| HidSharp | 2.6.4 | Apache-2.0；https://software.seekye.com/hidsharp |
| Mono.Posix.NETStandard | 1.0.0 | 上游 Mono 许可（MIT）；https://github.com/mono/mono |
| .NET Runtime | 8.0.28 | MIT 与随附第三方通知；https://github.com/dotnet/runtime |
| System.CodeDom / System.Management | 10.0.2 | MIT；https://github.com/dotnet/runtime |
| System.IO.Ports / System.Threading.AccessControl | 10.0.3 | MIT；https://github.com/dotnet/runtime |
| Windows App SDK | 1.7.250401001 | Microsoft Windows App SDK 许可及随附通知；https://github.com/microsoft/WindowsAppSDK |
| Microsoft.Web.WebView2 | 1.0.2903.40 | Microsoft WebView2 许可；https://aka.ms/webview |

上述硬件依赖使用未修改的 NuGet 二进制。LibreHardwareMonitor 0.9.6 对应源码提交为 `3d331e3370efb858411f19511373eff65a218701`；其源码及原始通知可在上游取得。相关 MPL 组件的源代码由表中上游仓库公开提供，组件修改需遵守其原始许可。许可证中的上游作者姓名/联系方式是原始公开版权声明，不是本项目开发者的私人数据，不应移除。

Windows 运行时及其底层第三方组件见 WindowsAppSDK-NOTICE、DotNet-THIRD-PARTY-NOTICES 和 LibreHardwareMonitor-THIRD-PARTY-NOTICES；其中 LibreHardwareMonitor 附带 PawnIO 模块通知。Inno Setup 只用于生成安装程序，本项目未在运行时调用其编译器；使用该构建工具应遵守其自身许可。
