# 发布说明

当前公开版本采用 v2.1.3，安装版和便携版共用相同主 EXE。

1. 运行控制台测试，再运行 scripts/publish.ps1；检查单文件 EXE 和安装编译结果。
2. 用正式 EXE 的 `--verify-package` 检查资源；`--smoke-dpi` 检查默认悬浮窗口和设置实际尺寸；`--smoke-ui` 检查任务栏实际挂载。冒烟运行使用独立实例键，不修改启动项、任务或用户配置。
3. 在 Git 索引上运行 `python scripts/audit-public.py`，确认不包含私人工作记录、配置、日志、诊断、备份和密钥。发布前也核对解压后的主程序文件，不能仅扫描压缩 EXE 表层。
4. Release 附件只含安装 EXE、便携 ZIP、校验文件；便携 ZIP 内含主 EXE、使用说明、MIT 和第三方许可证。不要上传整个本机 publish 目录或 verification/log 文件。
5. 使用 GitHub noreply 作者邮箱。发布 tag 必须指向已通过检查的源码提交；上传为 draft，确认完整后再公开 Release。

本次公开仓库仅收录当前使用、开发和接口文档。旧工作记录与源码备份保留在本地，不进入 Git 历史。旧安装版配置在新位置没有配置时自动复制迁移并保留原件；安装版卸载保留用户数据，受保护的硬件载荷暂不自动删除，任务清理权限仍需现场核对。

软件更新流程与 Release 固定结构见 [UPDATES.md](UPDATES.md)。发布前必须运行 `python scripts/validate-release.py 正文文件.md v2.1.3`，正文使用新增功能、优化改进、问题修复、更新说明、下载文件、验证情况六节；相同正文用于 GitHub Release 与应用内显示。版本从主 csproj 自动传给安装编译器。

Release 正文保存在 release-notes/版本.md。脚本支持 -ReleaseNotes 参数在构建前验证；发布产物的 release 子目录仅包含版本化安装包、便携 ZIP 与 SHA256SUMS.txt，可用于附件上传。
