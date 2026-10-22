# USBreeze

轻风一样简单的 Windows USB 存储设备测速工具。选择设备，点击一次，即可查看顺序写入、读取速度和变化曲线。

## 使用

支持 Windows 10/11 x64。便携版解压后运行 `USBreeze.exe`，不需要管理员权限。插入 U 盘、移动硬盘或 USB SSD，选择快速（256 MiB）、标准（1 GiB）或深入（4 GiB），点击「开始测速」。同一按钮可停止测试。最近记录可展开查看，并导出 CSV。

右上角语言按钮支持「跟随系统 / 简体中文 / English」。首次运行读取 Windows **显示语言**，而非地区或数字格式；当前支持简体中文和英语文化族，其他语言一律回退到英文（包括暂未支持的繁体中文）。手动选择优先于系统语言，并保存供下次运行使用。

## 如何理解成绩

成绩是电脑接口、线材、转接设备和存储设备共同作用下的**实际顺序读写吞吐量（MB/s，十进制）**，不是线材的额定上限，也不代表随机读写表现。比较线材时，固定硬盘、接口及测试大小，只更换线材。更快的线可能被较慢的硬盘或接口限制。

测试使用普通文件权限，在设备根目录创建唯一的 `.usbreeze-<随机标识>.tmp` 文件，独占打开，先写后读。只创建新文件，不覆盖已有文件，不执行裸盘写入、格式化或分区操作。文件句柄关闭时 Windows 自动删除测试文件，包括正常完成、停止、I/O 异常及进程退出；突然断电或拔盘仍可能留下文件。只清理本次创建的文件，不扫描删除其他文件。

测试绕过 Windows 文件缓存，使用对齐的直接 I/O 和写穿透；设备自身缓存、温度、固件、压缩/去重及后台读写仍可能影响结果。较小测试不能代表长期持续速度，测试会产生闪存写入量。运行中停止会等待当前系统 I/O 返回；若设备失去响应，不能保证立即停止。可用空间必须比测试文件大至少 512 MiB，FAT32 不支持深入 4 GiB 测试。

自动发现已挂载的 USB 存储卷和 Windows 标记的可移动存储卷，排除 Windows 系统卷。不支持手机 MTP、无盘符设备及网络磁盘。个别 USB4/雷电桥接设备可能以其他总线类型报告，首版可能无法自动识别。

## 本地数据与隐私

程序自身不联网、不收集遥测、不读取原有文件内容。设置和最多 20 条历史记录保存在 `%LOCALAPPDATA%\USBreeze`。历史记录含设备名称、盘符、时间、测试大小和速度；导出文件仅在用户选择时生成。

## 开发

原生 WPF / C# / .NET 10，无第三方运行时包依赖。

```powershell
dotnet build src/USBreeze.App/USBreeze.App.csproj -c Release
dotnet run --project tests/USBreeze.Checks -c Release
dotnet publish src/USBreeze.App/USBreeze.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/USBreeze-win-x64
```

检查程序只在指定工作目录写入 16 MiB 测试文件及一次最多 128 MiB 的取消测试文件，不会对外接 USB 设备自动测速。覆盖语言回退、设置保存、直接读写、停止清理、已有文件保护及历史记录。

便携自包含构建不要求用户安装 .NET；框架依赖构建需要 .NET 10 Desktop Runtime。GitHub Actions 在 Windows 上执行检查并生成便携 ZIP 构建产物。首版未做数字签名。

## English

USBreeze is a simple Windows USB storage benchmark. Pick a drive and measure sequential write/read speed, see live sparklines, stop a test, review recent results, and export CSV. The language menu defaults to the Windows display language and falls back to English for unsupported languages. Simplified Chinese and English are included.

Tests only use a new temporary file; they never overwrite existing files or write to a raw disk. Windows file caching is bypassed, but device caches and the entire hardware path still affect results. This measures storage-path throughput, not the rated maximum of a cable. Self-contained portable builds require no .NET installation.
