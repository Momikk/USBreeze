using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Microsoft.Win32;
using USBreeze.Core;

namespace USBreeze.App;

public partial class MainWindow : Window
{
    private static readonly long[] TestSizes = [256L << 20, 1L << 30, 4L << 30];
    private static readonly string[] SizeLabels = ["256 MiB", "1 GiB", "4 GiB"];
    private readonly string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "USBreeze");
    private readonly DispatcherTimer scanTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly Preferences preferences;
    private readonly List<BenchmarkResult> history;
    private readonly Texts text = Texts.Current;
    private CancellationTokenSource? cancellation;
    private bool scanning, initialized, closing, cancelRequested;
    private string stateKey = "Scanning";
    private SpeedSample? latest;
    private string? noticeKey;
    private int noticeCode;

    public MainWindow()
    {
        preferences = Preferences.Load(Path.Combine(dataDirectory, "preferences.json"));
        ApplyLanguage(); InitializeComponent();
        try { history = History.Load(Path.Combine(dataDirectory, "history.json")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { history = []; ShowNotice("HistoryLoadWarning"); }
        (preferences.Mode switch { 0 => QuickMode, 2 => ExtendedMode, _ => StandardMode }).IsChecked = true;
        WriteChart.Series = "Write"; ReadChart.Series = "Read";
        scanTimer.Tick += async (_, _) => await ScanAsync();
        initialized = true; RefreshLanguage();
    }
    private async void OnLoaded(object sender, RoutedEventArgs e) { await ScanAsync(); scanTimer.Start(); }
    private void ApplyLanguage() => text.Set(LanguageChoice.Resolve(preferences.Language, LanguageChoice.GetOsDisplayLanguage()));
    private void LanguageClick(object sender, RoutedEventArgs e)
    {
        SystemLanguage.IsChecked = preferences.Language == LanguageChoice.System;
        ChineseLanguage.IsChecked = preferences.Language == LanguageChoice.SimplifiedChinese;
        EnglishLanguage.IsChecked = preferences.Language == LanguageChoice.English;
        LanguageMenu.PlacementTarget = LanguageButton; LanguageMenu.Placement = PlacementMode.Bottom; LanguageMenu.IsOpen = true;
    }
    private void LanguageSelected(object sender, RoutedEventArgs e)
    {
        preferences.Language = (string)((MenuItem)sender).Tag;
        SystemLanguage.IsChecked = preferences.Language == LanguageChoice.System;
        ChineseLanguage.IsChecked = preferences.Language == LanguageChoice.SimplifiedChinese;
        EnglishLanguage.IsChecked = preferences.Language == LanguageChoice.English;
        LanguageMenu.IsOpen = false;
        ApplyLanguage(); RefreshLanguage(); SavePreferences();
    }
    private void RefreshLanguage()
    {
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(text.Culture.Name);
        RefreshDeviceText(); RefreshStatus(); RefreshHistory();
        ModeInfo.Text = text.Format("FileSize", SizeLabels[preferences.Mode]);
        StartLabel.Text = text[cancelRequested ? "Stopping" : cancellation is null ? "Start" : "Stop"];
        if (noticeKey is not null) ShowNotice(noticeKey, noticeCode);
    }
    private void SavePreferences()
    {
        try { preferences.Save(Path.Combine(dataDirectory, "preferences.json")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowNotice("SettingsWarning"); }
    }
    private void ModeChanged(object sender, RoutedEventArgs e)
    {
        if (!initialized) return;
        preferences.Mode = int.Parse((string)((RadioButton)sender).Tag, CultureInfo.InvariantCulture);
        ModeInfo.Text = text.Format("FileSize", SizeLabels[preferences.Mode]); SavePreferences();
    }
    private void DeviceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!initialized) return;
        RefreshDeviceText();
        if (cancellation is null)
        {
            stateKey = DevicePicker.SelectedItem is UsbDrive ? "Ready" : "Waiting";
            WriteValue.Text = ReadValue.Text = "—"; Progress.Value = 0;
            WriteChart.Clear(); ReadChart.Clear(); RefreshStatus();
        }
    }
    private void RefreshDeviceText()
    {
        if (DevicePicker.SelectedItem is UsbDrive drive)
        {
            DeviceInfo.Text = text.Format("DeviceInfo", drive.Format, drive.TotalBytes / 1e9, drive.FreeBytes / 1e9);
            ConnectedBadge.Visibility = Visibility.Visible;
        }
        else { DeviceInfo.Text = text["NoDevice"]; ConnectedBadge.Visibility = Visibility.Collapsed; }
        StartButton.IsEnabled = !cancelRequested && (cancellation is not null || DevicePicker.SelectedItem is UsbDrive);
    }
    private async Task ScanAsync()
    {
        if (scanning || cancellation is not null || closing) return;
        scanning = true;
        try
        {
            var selected = DevicePicker.SelectedItem as UsbDrive;
            var drives = await Task.Run(Devices.Discover);
            if (closing || cancellation is not null) return;
            var previous = (DevicePicker.ItemsSource as IReadOnlyList<UsbDrive>) ?? [];
            if (previous.Count != drives.Count || !previous.Zip(drives).All(pair =>
                pair.First.Root == pair.Second.Root && pair.First.Name == pair.Second.Name &&
                pair.First.TotalBytes == pair.Second.TotalBytes && pair.First.Format == pair.Second.Format))
            {
                DevicePicker.ItemsSource = drives;
                DevicePicker.SelectedItem = drives.FirstOrDefault(d => d.Root == selected?.Root) ?? drives.FirstOrDefault();
            }
            if (stateKey is "Scanning" or "ScanningFailed" or "Waiting") stateKey = drives.Count > 0 ? "Ready" : "Waiting";
            RefreshDeviceText(); RefreshStatus();
            if (DevicePicker.SelectedItem is UsbDrive current && drives.FirstOrDefault(d => d.Root == current.Root) is UsbDrive fresh)
                DeviceInfo.Text = text.Format("DeviceInfo", fresh.Format, fresh.TotalBytes / 1e9, fresh.FreeBytes / 1e9);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { stateKey = "ScanningFailed"; RefreshStatus(); }
        finally { scanning = false; }
    }
    private async void RefreshClick(object sender, RoutedEventArgs e) => await ScanAsync();
    private void RefreshStatus()
    {
        if (latest is not null && stateKey is "Writing" or "Reading")
            Status.Text = text.Format(stateKey, latest.Phase == "Write" ? latest.Fraction * 200 : (latest.Fraction - .5) * 200);
        else Status.Text = text[stateKey];
    }
    private async void StartClick(object sender, RoutedEventArgs e)
    {
        if (cancellation is not null)
        {
            cancelRequested = true; cancellation.Cancel(); stateKey = "Stopping";
            StartButton.IsEnabled = false; StartLabel.Text = text["Stopping"]; RefreshStatus(); return;
        }
        if (DevicePicker.SelectedItem is not UsbDrive selected) return;
        cancellation = new(); cancelRequested = false; latest = null;
        SetRunning(true); Notice.Visibility = Visibility.Collapsed; noticeKey = null; noticeCode = 0;
        WriteValue.Text = ReadValue.Text = "—"; WriteChart.Clear(); ReadChart.Clear(); Progress.Value = 0;
        stateKey = "Writing"; RefreshStatus();
        try
        {
            var devices = await Task.Run(Devices.Discover);
            if (!devices.Any(d => d.Root == selected.Root && d.Name == selected.Name && d.TotalBytes == selected.TotalBytes))
                throw new BenchmarkException(BenchmarkError.Unavailable);
            var observer = new Progress<SpeedSample>(sample =>
            {
                if (cancellation is null || cancelRequested) return;
                latest = sample; stateKey = sample.Phase == "Write" ? "Writing" : "Reading";
                (sample.Phase == "Write" ? WriteValue : ReadValue).Text = sample.MegabytesPerSecond.ToString("F1", text.Culture);
                (sample.Phase == "Write" ? WriteChart : ReadChart).Add(sample.Phase, sample.MegabytesPerSecond);
                Progress.Value = sample.Fraction; RefreshStatus();
            });
            var result = await Benchmark.RunAsync(selected.Root, selected.Name, TestSizes[preferences.Mode], observer, cancellation.Token);
            WriteValue.Text = result.WriteMBps.ToString("F1", text.Culture); ReadValue.Text = result.ReadMBps.ToString("F1", text.Culture);
            Progress.Value = 1; stateKey = "Done";
            history.Insert(0, result); if (history.Count > 20) history.RemoveRange(20, history.Count - 20);
            try { History.Save(Path.Combine(dataDirectory, "history.json"), history); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowNotice("HistoryWarning"); }
            RefreshHistory();
        }
        catch (OperationCanceledException) { stateKey = "Cancelled"; }
        catch (BenchmarkException ex)
        {
            stateKey = "ErrorTitle";
            ShowNotice(ex.Error switch { BenchmarkError.Write => "WriteError", BenchmarkError.Read => "ReadError", _ => ex.Error.ToString() }, ex.NativeCode);
        }
        catch (UnauthorizedAccessException) { stateKey = "ErrorTitle"; ShowNotice("ReadOnly"); }
        catch (IOException) { stateKey = "ErrorTitle"; ShowNotice("IoError"); }
        catch (Exception) { stateKey = "ErrorTitle"; ShowNotice("UnexpectedError"); }
        finally
        {
            cancellation.Dispose(); cancellation = null; cancelRequested = false;
            SetRunning(false); RefreshStatus();
        }
    }
    private void SetRunning(bool running)
    {
        DevicePicker.IsEnabled = RefreshButton.IsEnabled = QuickMode.IsEnabled = StandardMode.IsEnabled = ExtendedMode.IsEnabled = !running;
        StartLabel.Text = text[running ? "Stop" : "Start"]; StartGlyph.Text = running ? "\uE71A" : "\uE768";
        StartButton.IsEnabled = running || DevicePicker.SelectedItem is UsbDrive;
    }
    private void RefreshHistory()
    {
        HistoryEmpty.Visibility = history.Count == 0 ? Visibility.Visible : Visibility.Collapsed; ExportButton.IsEnabled = history.Count > 0;
        HistoryList.ItemsSource = history.Take(5).Select(r => new
        {
            Summary = text.Format("HistoryLine", r.Device, r.WriteMBps, r.ReadMBps),
            Detail = $"{r.Time.ToLocalTime().ToString("g", text.Culture)} · {r.TestBytes / (1024 * 1024)} MiB · {r.Root}"
        }).ToList();
    }
    private void ShowNotice(string key, int code = 0)
    {
        noticeKey = key; noticeCode = code; Notice.Text = text[key] + (code == 0 ? "" : " " + text.Format("Code", code)); Notice.Visibility = Visibility.Visible;
    }
    private void HelpClick(object sender, RoutedEventArgs e) => MessageBox.Show(this, text["HelpText"], text["Help"], MessageBoxButton.OK, MessageBoxImage.Information);
    private void ExportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = text["ExportTitle"], Filter = "CSV (*.csv)|*.csv", FileName = $"USBreeze-{DateTime.Now:yyyyMMdd-HHmm}.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            string Csv(string value) => "\"" + (value.Length > 0 && "=+-@\t\r\n".Contains(value[0]) ? "'" : "") + value.Replace("\"", "\"\"") + "\"";
            var rows = new List<string> { "Time,Device,Drive,TestBytes,WriteMBps,ReadMBps" };
            rows.AddRange(history.Select(r => string.Join(",", Csv(r.Time.ToString("O")), Csv(r.Device), Csv(r.Root), r.TestBytes.ToString(CultureInfo.InvariantCulture), r.WriteMBps.ToString("F2", CultureInfo.InvariantCulture), r.ReadMBps.ToString("F2", CultureInfo.InvariantCulture))));
            File.WriteAllLines(dialog.FileName, rows, new UTF8Encoding(true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ShowNotice("ExportFailed"); }
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (cancellation is not null)
        {
            e.Cancel = true; cancelRequested = true; cancellation.Cancel(); stateKey = "Stopping";
            StartLabel.Text = text["Stopping"]; StartButton.IsEnabled = false; RefreshStatus(); return;
        }
        closing = true; scanTimer.Stop();
    }
}
