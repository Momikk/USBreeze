using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace USBreeze.Core;

public sealed record SpeedSample(string Phase, double MegabytesPerSecond, double Fraction, double ElapsedSeconds);
public sealed record BenchmarkResult(DateTimeOffset Time, string Device, string Root, long TestBytes, double WriteMBps, double ReadMBps);

public static class Benchmark
{
    public const int BlockSize = 1024 * 1024;
    public const long ReserveBytes = 512L * 1024 * 1024;

    public static void Validate(string directory, long bytes)
    {
        if (bytes < BlockSize || bytes % BlockSize != 0) throw new BenchmarkException(BenchmarkError.InvalidSize);
        if (!Directory.Exists(directory)) throw new BenchmarkException(BenchmarkError.Unavailable);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new BenchmarkException(BenchmarkError.Redirected);
        var full = Path.GetFullPath(directory);
        if (full.StartsWith(@"\\", StringComparison.Ordinal)) throw new BenchmarkException(BenchmarkError.Network);
        var drive = new DriveInfo(Path.GetPathRoot(full)!);
        if (bytes > drive.AvailableFreeSpace - ReserveBytes) throw new BenchmarkException(BenchmarkError.NoSpace);
        if (drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase) && bytes >= uint.MaxValue)
            throw new BenchmarkException(BenchmarkError.Fat32);
    }

    public static Task<BenchmarkResult> RunAsync(string directory, string device, long bytes,
        IProgress<SpeedSample>? progress, CancellationToken token) =>
        Task.Run(() => Run(directory, device, bytes, progress, token), token);

    private static unsafe BenchmarkResult Run(string directory, string device, long bytes,
        IProgress<SpeedSample>? progress, CancellationToken token)
    {
        Validate(directory, bytes);
        token.ThrowIfCancellationRequested();
        var root = Path.GetPathRoot(Path.GetFullPath(directory))!;
        if (!Native.GetDiskFreeSpace(root, out _, out var sector, out _, out _) || sector == 0 ||
            sector > 65536 || BlockSize % sector != 0 || 65536 % sector != 0)
            throw new BenchmarkException(BenchmarkError.Alignment);

        var path = Path.Combine(directory, $".usbreeze-{Guid.NewGuid():N}.tmp");
        var pointer = NativeMemory.AlignedAlloc(BlockSize, 65536);
        if (pointer == null) throw new OutOfMemoryException();
        try
        {
            RandomNumberGenerator.Fill(new Span<byte>(pointer, BlockSize));
            // CREATE_NEW prevents overwrites. Exclusive handle and DELETE_ON_CLOSE protect cleanup,
            // including process termination. NO_BUFFERING avoids Windows file-cache read scores.
            using var file = Native.CreateFile(path, 0xC0010000, 0, IntPtr.Zero, 1,
                0xA4000080, IntPtr.Zero); // write-through | no-buffering | delete-on-close | normal
            if (file.IsInvalid) ThrowIo(BenchmarkError.Create);
            double Transfer(bool write)
            {
                var watch = Stopwatch.StartNew();
                double lastReportTime = 0;
                long lastReportBytes = 0;
                for (long done = 0; done < bytes;)
                {
                    token.ThrowIfCancellationRequested();
                    uint transferred;
                    var ok = write
                        ? Native.WriteFile(file, (IntPtr)pointer, BlockSize, out transferred, IntPtr.Zero)
                        : Native.ReadFile(file, (IntPtr)pointer, BlockSize, out transferred, IntPtr.Zero);
                    if (!ok) ThrowIo(write ? BenchmarkError.Write : BenchmarkError.Read);
                    if (transferred != BlockSize) throw new BenchmarkException(BenchmarkError.Incomplete);
                    done += transferred;
                    var seconds = watch.Elapsed.TotalSeconds;
                    if (seconds - lastReportTime >= .12 || done == bytes)
                    {
                        var speed = (done - lastReportBytes) / 1e6 / Math.Max(seconds - lastReportTime, .000001);
                        progress?.Report(new(write ? "Write" : "Read", speed,
                            (write ? 0 : .5) + .5 * done / bytes, seconds));
                        lastReportTime = seconds;
                        lastReportBytes = done;
                    }
                }
                if (write && !Native.FlushFileBuffers(file)) ThrowIo(BenchmarkError.Flush);
                var average = bytes / 1e6 / watch.Elapsed.TotalSeconds;
                progress?.Report(new(write ? "Write" : "Read", average, write ? .5 : 1, watch.Elapsed.TotalSeconds));
                return average;
            }

            var writeSpeed = Transfer(true);
            token.ThrowIfCancellationRequested();
            if (!Native.SetFilePointerEx(file, 0, out _, 0)) ThrowIo(BenchmarkError.Seek);
            var readSpeed = Transfer(false);
            token.ThrowIfCancellationRequested();
            return new(DateTimeOffset.Now, device, root, bytes, writeSpeed, readSpeed);
        }
        finally { NativeMemory.AlignedFree(pointer); }
    }

    private static void ThrowIo(BenchmarkError error) =>
        throw new BenchmarkException(error, Marshal.GetLastWin32Error());
}
