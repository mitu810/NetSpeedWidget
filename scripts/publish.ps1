param(
    [string]$OutputDirectory,
    [string]$InstallerCompiler,
    [string]$ReleaseNotes
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$appProject = Get-Content -LiteralPath (Join-Path $projectRoot 'NetSpeedWidget.csproj') -Encoding UTF8
$appVersion = [string]$appProject.Project.PropertyGroup.Version
if ($ReleaseNotes) {
    & python (Join-Path $projectRoot 'scripts\validate-release.py') $ReleaseNotes "v$appVersion"
    if ($LASTEXITCODE -ne 0) { throw 'Release 更新内容不符合格式，已停止发布构建。' }
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot ('publish\NetSpeedWidget-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw "输出目录已存在，请选择新目录以保留已有发布包：$OutputDirectory" }
$stagingRoot = Join-Path $projectRoot ('artifacts\publish-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$helperDirectory = Join-Path $stagingRoot 'Hardware'
$helperArchive = Join-Path $stagingRoot 'Hardware.zip'
New-Item -ItemType Directory -Path $stagingRoot -Force | Out-Null

# 1. 辅助进程以目录形式自包含发布，授权后将完整依赖复制到受保护目录。
& dotnet publish (Join-Path $projectRoot 'NetSpeedWidget.Hardware\NetSpeedWidget.Hardware.csproj') -c Release -r win-x64 --self-contained true -o $helperDirectory -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw '硬件辅助进程发布失败。' }
[IO.Compression.ZipFile]::CreateFromDirectory($helperDirectory, $helperArchive)

# 更新程序自身为独立自包含 EXE；等待原程序退出后处理安装或便携替换。
$updaterDirectory = Join-Path $stagingRoot 'Updater'
& dotnet publish (Join-Path $projectRoot 'NetSpeedWidget.Updater\NetSpeedWidget.Updater.csproj') -c Release -r win-x64 --self-contained true -o $updaterDirectory -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw '更新辅助程序发布失败。' }
$updaterExe = Join-Path $updaterDirectory 'NetSpeedWidget.Updater.exe'

# 2. 辅助进程目录嵌入主 EXE，用户仅需双击主程序；不依赖开发机运行时。
$packageCacheOutput = & dotnet nuget locals global-packages --list
if ($LASTEXITCODE -ne 0) { throw '无法确定 NuGet 构建工具目录。' }
$packageCache = ($packageCacheOutput -replace '^global-packages:\s*', '').Trim()
$msixTasks = Join-Path $packageCache 'microsoft.windows.sdk.buildtools.msix\1.7.260610101\tools\net6.0\'
& dotnet publish (Join-Path $projectRoot 'NetSpeedWidget.csproj') -p:PublishProfile=SingleFile -p:Platform=x64 "-p:HardwareHelperPath=$helperArchive" "-p:UpdaterHelperPath=$updaterExe" "-p:MsixTaskAssemblyLocation=$msixTasks" -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw '主程序单文件发布失败。' }
if (-not (Test-Path -LiteralPath (Join-Path $OutputDirectory 'NetSpeedWidget.exe'))) { throw '发布产物缺少主 EXE。' }

# 3. 两种发行版均附带项目许可证与原始第三方通知。
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination $OutputDirectory
Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses') -Destination $OutputDirectory -Recurse

# 3. 同一主 EXE 同时作为安装版的载荷。编译器只用于开发环境，用户不需要安装。
if (-not $InstallerCompiler) {
    $installerCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    $innoKeys = @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1', 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1')
    $innoLocations = Get-ItemProperty -LiteralPath $innoKeys -ErrorAction SilentlyContinue | ForEach-Object { if ($_.InstallLocation) { Join-Path $_.InstallLocation 'ISCC.exe' } }
    $candidates = @($installerCommand.Source) + @($innoLocations) + @((Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'), (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'))
    $InstallerCompiler = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
}
if ($InstallerCompiler) {
    & $InstallerCompiler "/DAppVersion=$appVersion" "/DSourceDirectory=$OutputDirectory" "/DOutputDirectory=$OutputDirectory" (Join-Path $projectRoot 'installer\NetSpeedWidget.iss')
    if ($LASTEXITCODE -ne 0) { throw '安装程序编译失败。' }
} else { Write-Warning '未发现 Inno Setup，便携 EXE 已生成；安装脚本保留，尚未生成安装程序。' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $OutputDirectory '使用说明.md')
# 4. Release 附件使用固定命名，便携 ZIP 只收录明确的发行白名单。
$releaseDirectory = Join-Path $OutputDirectory 'release'
New-Item -ItemType Directory -Path $releaseDirectory | Out-Null
$portableName = "NetSpeedWidget-v$appVersion-win-x64-Portable.zip"
$portablePath = Join-Path $releaseDirectory $portableName
$archive = [IO.Compression.ZipFile]::Open($portablePath, [IO.Compression.ZipArchiveMode]::Create)
try {
    $publicFiles = @('NetSpeedWidget.exe', 'LICENSE', 'THIRD_PARTY_NOTICES.md', '使用说明.md')
    $publicFiles += @(Get-ChildItem -LiteralPath (Join-Path $OutputDirectory 'licenses') -File | ForEach-Object { 'licenses/' + $_.Name })
    foreach ($name in $publicFiles) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $OutputDirectory $name), $name, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
if (Test-Path -LiteralPath (Join-Path $OutputDirectory 'NetSpeedWidget-Setup.exe')) {
    Copy-Item -LiteralPath (Join-Path $OutputDirectory 'NetSpeedWidget-Setup.exe') -Destination (Join-Path $releaseDirectory "NetSpeedWidget-v$appVersion-win-x64-Setup.exe")
}
$checksums = @(Get-ChildItem -LiteralPath $releaseDirectory -File | Sort-Object Name | ForEach-Object {
    (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
})
[IO.File]::WriteAllText((Join-Path $releaseDirectory 'SHA256SUMS.txt'), ($checksums -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
$releaseFiles = @(Get-ChildItem -LiteralPath $OutputDirectory -Filter *.exe | ForEach-Object {
    [PSCustomObject]@{ File = $_.Name; SizeBytes = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$releaseFiles | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'release-manifest.json') -Encoding UTF8
$releaseFiles
