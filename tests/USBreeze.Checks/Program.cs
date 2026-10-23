using System.Globalization;
using System.Text.Json;
using USBreeze.Core;
using USBreeze.App;

static void Require(bool value, string message) { if (!value) throw new Exception(message); }
var folder = Path.Combine(Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/checks"), Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var sentinel = Path.Combine(folder, "existing-user-file.txt");
await File.WriteAllTextAsync(sentinel, "must remain unchanged");

foreach (var (culture, expected) in new[] { ("zh-CN", "zh-Hans"), ("zh-SG", "zh-Hans"), ("zh-Hans", "zh-Hans"), ("en-US", "en"), ("en-GB", "en"), ("ja-JP", "en"), ("de-DE", "en"), ("zh-TW", "en"), ("zh-HK", "en"), ("fr-FR", "en"), ("", "en") })
    Require(LanguageChoice.Resolve("system", CultureInfo.GetCultureInfo(culture)) == expected, $"OS language fallback: {culture}");
Require(LanguageChoice.Resolve("en", CultureInfo.GetCultureInfo("zh-CN")) == "en", "Manual English overrides OS");
Require(LanguageChoice.Resolve("zh-Hans", CultureInfo.GetCultureInfo("ja-JP")) == "zh-Hans", "Manual Chinese overrides OS");
Require(LanguageChoice.Resolve("unsupported", CultureInfo.GetCultureInfo("ja-JP")) == "en", "Invalid preference falls back safely");
Console.WriteLine("PASS: OS display language, supported families, English fallback and manual override");

foreach (var entry in Texts.Catalog)
{
    Require(!string.IsNullOrWhiteSpace(entry.Value.English) && !string.IsNullOrWhiteSpace(entry.Value.Chinese), "Missing translation: " + entry.Key);
    Require(System.Text.RegularExpressions.Regex.Matches(entry.Value.English, @"\{\d+").Count == System.Text.RegularExpressions.Regex.Matches(entry.Value.Chinese, @"\{\d+").Count, "Translation format mismatch: " + entry.Key);
}
var notifications = 0;
Texts.Current.PropertyChanged += (_, _) => notifications++;
Texts.Current.Set("zh-Hans"); Require(Texts.Current["Start"] == "开始测速", "Chinese UI string");
Texts.Current.Set("en"); Require(Texts.Current["Start"] == "Start test" && notifications == 2, "Live language updates");
Console.WriteLine($"PASS: {Texts.Catalog.Count} bilingual strings and live change notifications");

var prefPath = Path.Combine(folder, "preferences.json");
Require(Preferences.Load(prefPath).Language == "system", "First launch follows OS");
var preferences = new Preferences { Language = "zh-Hans", Mode = 2 }; preferences.Save(prefPath);
Require(Preferences.Load(prefPath).Language == "zh-Hans" && Preferences.Load(prefPath).Mode == 2, "Persist settings");
await File.WriteAllTextAsync(prefPath, "{\"Language\":\"xx\",\"Mode\":99}");
Require(Preferences.Load(prefPath).Language == "system" && Preferences.Load(prefPath).Mode == 2, "Sanitize unsupported settings");
await File.WriteAllTextAsync(prefPath, "not json"); Require(Preferences.Load(prefPath).Language == "system", "Recover malformed settings");
Console.WriteLine("PASS: default settings, persistence and malformed setting recovery");

foreach (var size in new long[] { -1, 0, Benchmark.BlockSize - 1 })
{
    try { Benchmark.Validate(folder, size); throw new Exception("Invalid size accepted"); }
    catch (BenchmarkException ex) when (ex.Error == BenchmarkError.InvalidSize) { }
}
var free = new DriveInfo(Path.GetPathRoot(folder)!).AvailableFreeSpace;
try { Benchmark.Validate(folder, (free / Benchmark.BlockSize + 1) * Benchmark.BlockSize); throw new Exception("No-space check failed"); }
catch (BenchmarkException ex) when (ex.Error == BenchmarkError.NoSpace) { }
Console.WriteLine("PASS: invalid-size and free-space guards before writes");

var samples = new List<SpeedSample>();
var result = await Benchmark.RunAsync(folder, "Workspace test fixture", 16L * Benchmark.BlockSize, new InlineProgress(s => samples.Add(s)), CancellationToken.None);
Require(result.ReadMBps > 0 && result.WriteMBps > 0 && double.IsFinite(result.ReadMBps), "Positive measured throughput");
Require(samples.Any(s => s.Phase == "Write") && samples.Any(s => s.Phase == "Read") && samples.Last().Fraction == 1, "Both test stages complete");
Require(samples.Zip(samples.Skip(1)).All(pair => pair.First.Fraction <= pair.Second.Fraction), "Monotonic progress");
Require(!Directory.EnumerateFiles(folder, ".usbreeze-*.tmp").Any(), "Success cleanup");
Console.WriteLine("PASS: real 16 MiB direct file I/O in workspace only; both stages and automatic cleanup");

using var cts = new CancellationTokenSource();
try
{
    await Benchmark.RunAsync(folder, "Cancellation fixture", 128L * Benchmark.BlockSize,
        new InlineProgress(s => { if (s.Phase == "Write") cts.Cancel(); }), cts.Token);
    throw new Exception("Cancellation was ignored");
}
catch (OperationCanceledException) { }
Require(!Directory.EnumerateFiles(folder, ".usbreeze-*.tmp").Any(), "Cancellation cleanup");
Require(await File.ReadAllTextAsync(sentinel) == "must remain unchanged", "Existing files preserved");
Console.WriteLine("PASS: cancel during write, automatic cleanup, existing file preserved");

Require(TransferPlan.For(TestMode.LargeFile).FileCount == 1 && TransferPlan.For(TestMode.LargeFile).TotalBytes == 1L << 30, "Large-file workload");
Require(TransferPlan.For(TestMode.SmallFiles).FileCount == 1024 && TransferPlan.For(TestMode.SmallFiles).BytesPerFile == 65536, "Small-file workload");
foreach (var plan in new[] { new TransferPlan(TestMode.LargeFile, 1, 16 * Benchmark.BlockSize), new TransferPlan(TestMode.SmallFiles, 16, 65536) })
{
    var trace = new List<SpeedSample>();
    var observedCount = 0;
    var transfer = await Benchmark.RunAsync(folder, "File transfer fixture", plan, new InlineProgress(s =>
    {
        trace.Add(s);
        if (s.Finishing) observedCount = Directory.GetFiles(folder, "*.tmp", SearchOption.AllDirectories).Length;
    }), CancellationToken.None);
    Require(observedCount == plan.FileCount, "Actual file count must match chosen workload");
    Require(transfer.Mode == plan.Mode.ToString() && transfer.FileCount == plan.FileCount, "Mode retained in result");
    Require(transfer.ReadSeconds > 0 && transfer.WriteSeconds > 0 && transfer.ReadMBps > 0, "Both timed phases complete");
    Require(Math.Abs(transfer.TestBytes / 1e6 / transfer.WriteSeconds - transfer.WriteMBps) < .00001, "Average includes write completion wait");
    Require(trace.Any(s => s.Finishing) && trace.Any(s => s.Phase == "Read") && trace.Last().Fraction == 1, "Flush and read stages observed");
    Require(trace.Zip(trace.Skip(1)).All(pair => pair.First.Fraction <= pair.Second.Fraction), "File-mode progress monotonic");
    Require(!Directory.EnumerateFileSystemEntries(folder, ".usbreeze-*").Any(), "File-mode files and folder cleaned");
    foreach (var cancelPhase in new[] { "Write", "Read" })
    {
        using var abort = new CancellationTokenSource();
        try
        {
            await Benchmark.RunAsync(folder, "File-mode cancellation", plan,
                new InlineProgress(s => { if (s.Phase == cancelPhase) abort.Cancel(); }), abort.Token);
            throw new Exception("File-mode cancellation ignored");
        }
        catch (OperationCanceledException) { }
        Require(!Directory.EnumerateFileSystemEntries(folder, ".usbreeze-*").Any(), "File-mode cancellation cleanup");
    }
}
Require(await File.ReadAllTextAsync(sentinel) == "must remain unchanged", "File modes preserved existing data");
var legacy = JsonSerializer.Deserialize<BenchmarkResult>("{\"Time\":\"2026-10-22T00:00:00Z\",\"Device\":\"old\",\"Root\":\"E:\\\\\",\"TestBytes\":1024,\"WriteMBps\":1,\"ReadMBps\":2}");
Require(legacy?.Mode == "Quick" && legacy.FileCount == 1, "Old results remain readable");
Console.WriteLine("PASS: large-file / small-file I/O, counts, write completion timing, read/write cancellation, cleanup and old result compatibility");

var historyPath = Path.Combine(folder, "history.json");
History.Save(historyPath, Enumerable.Repeat(result, 25));
Require(History.Load(historyPath).Count == 20, "History capped at 20");
Require(History.Load(historyPath)[0].ReadMBps == result.ReadMBps, "History roundtrip");
Require(!Directory.EnumerateFiles(folder, "*.tmp").Any(), "No leftover settings/history files");
Console.WriteLine("PASS: atomic history persistence and retention");
var discovered = Devices.Discover();
Require(discovered.All(d => !d.Root.Equals(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)), StringComparison.OrdinalIgnoreCase)), "Exclude OS volume");
Console.WriteLine("PASS: device discovery excludes OS volume; no USB benchmark was run");
await File.WriteAllTextAsync(Path.Combine(folder, "passed.json"), JsonSerializer.Serialize(new { passed = true, languageCases = 14, strings = Texts.Catalog.Count, usbBenchmarkRun = false }));
Console.WriteLine("All checks passed.");

sealed class InlineProgress(Action<SpeedSample> report) : IProgress<SpeedSample>
{
    public void Report(SpeedSample value) => report(value);
}
