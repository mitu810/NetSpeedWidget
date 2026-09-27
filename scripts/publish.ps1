param(
    [string]$OutputDirectory,
    [string]$InstallerCompiler
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
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

# 2. 辅助进程目录嵌入主 EXE，用户仅需双击主程序；不依赖开发机运行时。
$packageCacheOutput = & dotnet nuget locals global-packages --list
if ($LASTEXITCODE -ne 0) { throw '无法确定 NuGet 构建工具目录。' }
$packageCache = ($packageCacheOutput -replace '^global-packages:\s*', '').Trim()
$msixTasks = Join-Path $packageCache 'microsoft.windows.sdk.buildtools.msix\1.7.260610101\tools\net6.0\'
& dotnet publish (Join-Path $projectRoot 'NetSpeedWidget.csproj') -p:PublishProfile=SingleFile -p:Platform=x64 "-p:HardwareHelperPath=$helperArchive" "-p:MsixTaskAssemblyLocation=$msixTasks" -o $OutputDirectory
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
    & $InstallerCompiler "/DSourceDirectory=$OutputDirectory" "/DOutputDirectory=$OutputDirectory" (Join-Path $projectRoot 'installer\NetSpeedWidget.iss')
    if ($LASTEXITCODE -ne 0) { throw '安装程序编译失败。' }
} else { Write-Warning '未发现 Inno Setup，便携 EXE 已生成；安装脚本保留，尚未生成安装程序。' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $OutputDirectory '使用说明.md')
$releaseFiles = @(Get-ChildItem -LiteralPath $OutputDirectory -Filter *.exe | ForEach-Object {
    [PSCustomObject]@{ File = $_.Name; SizeBytes = $_.Length; SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$releaseFiles | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'release-manifest.json') -Encoding UTF8
$releaseFiles
