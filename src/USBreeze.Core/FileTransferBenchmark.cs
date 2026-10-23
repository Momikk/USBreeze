using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace USBreeze.Core;

public static class FileTransferBenchmark
{
    public static Task<BenchmarkResult> RunAsync(string directory, string device, TransferPlan plan,
        IProgress<SpeedSample>? progress, CancellationToken token) => Task.Run(() => Run(directory, device, plan, progress, token), token);

    private static unsafe BenchmarkResult Run(string directory, string device, TransferPlan plan,
        IProgress<SpeedSample>? progress, CancellationToken token)
    {
        if (plan.Mode is not (TestMode.LargeFile or TestMode.SmallFiles) || plan.FileCount is < 1 or > 4096 ||
            plan.BytesPerFile < 65536 || plan.BytesPerFile % 65536 != 0)
            throw new BenchmarkException(BenchmarkError.InvalidSize);
        Benchmark.Validate(directory, plan.TotalBytes);
        token.ThrowIfCancellationRequested();
        var root = Path.GetPathRoot(Path.GetFullPath(directory))!;
        if (!Native.GetDiskFreeSpace(root, out _, out var sector, out _, out _) || sector == 0 ||
            sector > 65536 || 65536 % sector != 0) throw new BenchmarkException(BenchmarkError.Alignment);
        var folder = Path.Combine(Path.GetFullPath(directory), $".usbreeze-{Guid.NewGuid():N}.tmp");
        if (Directory.Exists(folder) || File.Exists(folder)) throw new BenchmarkException(BenchmarkError.Create);
        var pointer = NativeMemory.AlignedAlloc(Benchmark.BlockSize, 65536);
        if (pointer == null) throw new OutOfMemoryException();
        var handles = new List<(string Path, SafeFileHandle Handle)>();
        var created = false;
        try
        {
            RandomNumberGenerator.Fill(new Span<byte>(pointer, Benchmark.BlockSize));
            Directory.CreateDirectory(folder); created = true;
            var writeWatch = Stopwatch.StartNew();
            long done = 0, reported = 0;
            double reportedAt = 0;
            void Report(string phase, Stopwatch watch, bool force = false, bool finishing = false)
            {
                var seconds = watch.Elapsed.TotalSeconds;
                if (!force && seconds - reportedAt < .15) return;
                var rate = (done - reported) / 1e6 / Math.Max(seconds - reportedAt, .000001);
                progress?.Report(new(phase, rate, (phase == "Write" ? 0 : .5) + .5 * done / plan.TotalBytes, seconds, finishing));
                reported = done; reportedAt = seconds;
            }
            for (var index = 0; index < plan.FileCount; index++)
            {
                token.ThrowIfCancellationRequested();
                var path = Path.Combine(folder, $"{index:D5}.tmp");
                // Ordinary OS-buffered writes, without forcing every write through to media.
                // Keep an owning DELETE_ON_CLOSE handle until reads finish or cancellation occurs.
                var handle = Native.CreateFile(path, 0x40010000, 5, IntPtr.Zero, 1, 0x04000080, IntPtr.Zero);
                if (handle.IsInvalid) { handle.Dispose(); ThrowIo(BenchmarkError.Create); }
                handles.Add((path, handle));
                for (long offset = 0; offset < plan.BytesPerFile;)
                {
                    token.ThrowIfCancellationRequested();
                    var count = (uint)Math.Min(Benchmark.BlockSize, plan.BytesPerFile - offset);
                    if (!Native.WriteFile(handle, (IntPtr)pointer, count, out var written, IntPtr.Zero)) ThrowIo(BenchmarkError.Write);
                    if (written != count) throw new BenchmarkException(BenchmarkError.Incomplete);
                    done += written; offset += written; Report("Write", writeWatch);
                }
            }
            // Include the completion wait in the write average. Do not report a cache-only score.
            Report("Write", writeWatch, true, true);
            foreach (var item in handles)
            {
                token.ThrowIfCancellationRequested();
                if (!Native.FlushFileBuffers(item.Handle)) ThrowIo(BenchmarkError.Flush);
            }
            var writeSeconds = writeWatch.Elapsed.TotalSeconds;
            var writeSpeed = plan.TotalBytes / 1e6 / writeSeconds;
            progress?.Report(new("Write", writeSpeed, .5, writeSeconds));
            token.ThrowIfCancellationRequested();
            done = reported = 0; reportedAt = 0;
            var readWatch = Stopwatch.StartNew();
            foreach (var item in handles)
            {
                token.ThrowIfCancellationRequested();
                // Reopen each file for reads. Bypass the data just left in Windows memory by writes.
                // This is a storage read estimate, not a copy test against another source/destination drive.
                using var reader = Native.CreateFile(item.Path, 0x80000000, 7, IntPtr.Zero, 3, 0x20000080, IntPtr.Zero);
                if (reader.IsInvalid) ThrowIo(BenchmarkError.Read);
                for (long offset = 0; offset < plan.BytesPerFile;)
                {
                    token.ThrowIfCancellationRequested();
                    var count = (uint)Math.Min(Benchmark.BlockSize, plan.BytesPerFile - offset);
                    if (!Native.ReadFile(reader, (IntPtr)pointer, count, out var read, IntPtr.Zero)) ThrowIo(BenchmarkError.Read);
                    if (read != count) throw new BenchmarkException(BenchmarkError.Incomplete);
                    done += read; offset += read; Report("Read", readWatch);
                }
            }
            var readSeconds = readWatch.Elapsed.TotalSeconds;
            var readSpeed = plan.TotalBytes / 1e6 / readSeconds;
            progress?.Report(new("Read", readSpeed, 1, readSeconds));
            token.ThrowIfCancellationRequested();
            return new(DateTimeOffset.Now, device, root, plan.TotalBytes, writeSpeed, readSpeed,
                plan.Mode.ToString(), plan.FileCount, writeSeconds, readSeconds);
        }
        finally
        {
            foreach (var item in handles) item.Handle.Dispose();
            NativeMemory.AlignedFree(pointer);
            // Delete only the exact empty folder created by this run; never recurse over user files.
            if (created && Directory.Exists(folder))
            {
                try { Directory.Delete(folder, false); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { throw new BenchmarkException(BenchmarkError.Cleanup); }
            }
        }
    }

    private static void ThrowIo(BenchmarkError error) => throw new BenchmarkException(error, Marshal.GetLastWin32Error());
}
