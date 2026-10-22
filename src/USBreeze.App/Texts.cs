using System.ComponentModel;
using System.Globalization;
using USBreeze.Core;

namespace USBreeze.App;

public sealed class Texts : INotifyPropertyChanged
{
    public static Texts Current { get; } = new();
    public static IReadOnlyDictionary<string, (string English, string Chinese)> Catalog { get; } = new Dictionary<string, (string, string)>
    {
        ["Title"] = ("USBreeze · USB speed test", "USBreeze · USB 读写测速"),
        ["Headline"] = ("Plug it in. See how fast it goes.", "插上设备，测一下就知道。"),
        ["Write"] = ("Write speed", "写入速度"), ["Read"] = ("Read speed", "读取速度"),
        ["Quick"] = ("Quick", "快速"), ["Standard"] = ("Standard", "标准"), ["Extended"] = ("Extended", "深入"),
        ["Start"] = ("Start test", "开始测速"), ["Stop"] = ("Stop test", "停止测速"),
        ["Stopping"] = ("Stopping…", "正在停止…"), ["Ready"] = ("Ready when you are", "准备就绪"),
        ["Waiting"] = ("Connect a USB drive to get started", "连接 U 盘或移动硬盘即可开始"),
        ["Scanning"] = ("Looking for USB drives…", "正在查找 USB 存储设备…"),
        ["Connected"] = ("● Connected", "● 已连接"), ["NoDevice"] = ("No USB drive connected", "未连接 USB 存储设备"),
        ["Device"] = ("USB storage device", "USB 存储设备"), ["Refresh"] = ("Refresh devices", "刷新设备"),
        ["DeviceInfo"] = ("{0} · {1:0} GB · {2:0.0} GB available", "{0} · {1:0} GB · 可用 {2:0.0} GB"),
        ["FileSize"] = ("{0} temporary test file", "{0} 临时测试文件"),
        ["Writing"] = ("Writing · {0:0}%", "正在写入 · {0:0}%"), ["Reading"] = ("Reading · {0:0}%", "正在读取 · {0:0}%"),
        ["Done"] = ("Test complete · Temporary file removed", "测试完成 · 临时文件已清理"),
        ["Cancelled"] = ("Stopped · Temporary file removed", "已停止 · 临时文件已清理"),
        ["Recent"] = ("Recent results", "最近测试"), ["NoHistory"] = ("No results yet. Swap a cable and compare.", "暂无记录。换线后再测一次，轻松比较速度。"),
        ["HistoryLine"] = ("{0} · Write {1:F1} / Read {2:F1} MB/s", "{0} · 写入 {1:F1} / 读取 {2:F1} MB/s"),
        ["Export"] = ("Export CSV", "导出 CSV"), ["ExportTitle"] = ("Export test results", "导出测试结果"),
        ["FooterSafety"] = ("Temporary file only · Existing files stay untouched", "仅使用临时文件 · 不覆盖原有文件"),
        ["FooterScope"] = ("Measures the whole connection, not a cable's rated limit", "实测整套连接速度，非线材额定速度"),
        ["LanguageButton"] = ("Language", "语言"), ["FollowSystem"] = ("Follow system", "跟随系统"),
        ["SettingsWarning"] = ("Preferences could not be saved. Changes apply to this session.", "设置无法保存，本次修改在当前会话中生效。"),
        ["HistoryWarning"] = ("Test complete, but the result could not be saved.", "测速已完成，但结果无法保存。"),
        ["HistoryLoadWarning"] = ("Saved results could not be loaded.", "无法读取已保存的测试记录。"),
        ["ReadOnly"] = ("The device is read-only or you don't have write permission.", "设备为只读，或当前账号没有写入权限。"),
        ["IoError"] = ("The device became unavailable or an I/O error occurred. Reconnect and try again.", "设备不可用或发生读写错误，请重新连接后再试。"),
        ["UnexpectedError"] = ("The test could not finish. Reconnect the device and try again.", "测试未能完成，请重新连接设备后再试。"),
        ["ErrorTitle"] = ("Test stopped", "测试已停止"), ["Code"] = ("Windows error: {0}", "Windows 错误码：{0}"),
        ["InvalidSize"] = ("Choose a supported test size.", "请选择支持的测试大小。"),
        ["Unavailable"] = ("The selected device is no longer available. Refresh and select it again.", "所选设备已不可用，请刷新后重新选择。"),
        ["Redirected"] = ("Redirected folders cannot be used for this test.", "不支持重定向的测试文件夹。"),
        ["Network"] = ("Network drives are not supported.", "不支持网络磁盘测速。"),
        ["NoSpace"] = ("Not enough free space. The test must leave at least 512 MiB free.", "可用空间不足，测试时需保留至少 512 MiB 空间。"),
        ["Fat32"] = ("FAT32 cannot hold a 4 GiB file. Choose Quick or Standard.", "FAT32 不支持 4 GiB 文件，请选择快速或标准测试。"),
        ["Alignment"] = ("The device's direct I/O alignment could not be confirmed. No test was run.", "无法确认设备的直接读写对齐要求，已停止测速。"),
        ["Create"] = ("Cannot create the test file. Check write access and device connection.", "无法创建测试文件，请检查写入权限和设备连接。"),
        ["WriteError"] = ("Writing failed. Check the device connection.", "写入失败，请检查设备连接。"),
        ["ReadError"] = ("Reading failed. Check the device connection.", "读取失败，请检查设备连接。"),
        ["Incomplete"] = ("The device returned incomplete data. The test was stopped.", "设备返回的数据长度不完整，测试已停止。"),
        ["Flush"] = ("The device could not finish flushing writes.", "设备无法完成写入刷新。"),
        ["Seek"] = ("The test file could not be positioned for reading.", "无法定位测试文件进行读取。"),
        ["Help"] = ("About this test", "测试说明"),
        ["HelpText"] = ("A sequential write test followed by a read test of the same temporary file. Windows file caching is bypassed; device caches, temperature and other activity can still affect results. Use a larger test to observe sustained speed. To compare cables, keep the same drive, port and test size. Tests write data and count toward flash wear.", "先顺序写入，再读取同一临时文件。测试绕过 Windows 文件缓存，但设备内部缓存、温度和其他读写活动仍会影响结果。较大测试适合观察持续速度。比较线材时，请保持硬盘、接口和测试大小一致。测试会写入数据，并计入闪存写入量。"),
        ["ExportFailed"] = ("Could not export the results. Choose another location.", "无法导出测试结果，请选择其他位置。"),
        ["ScanningFailed"] = ("Could not list devices. Click refresh to try again.", "无法获取设备列表，请点击刷新重试。")
    };

    public string Language { get; private set; } = LanguageChoice.English;
    public CultureInfo Culture => CultureInfo.GetCultureInfo(Language == LanguageChoice.SimplifiedChinese ? "zh-CN" : "en-US");
    public string this[string key] => Catalog.TryGetValue(key, out var value) ? Language == LanguageChoice.SimplifiedChinese ? value.Chinese : value.English : key;
    public string Format(string key, params object[] values) => string.Format(Culture, this[key], values);
    public void Set(string language) { Language = language; PropertyChanged?.Invoke(this, new("Item[]")); }
    public event PropertyChangedEventHandler? PropertyChanged;
}
