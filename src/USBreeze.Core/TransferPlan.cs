namespace USBreeze.Core;

public enum TestMode { Quick, LargeFile, SmallFiles }

public sealed record TransferPlan(TestMode Mode, int FileCount, int BytesPerFile)
{
    public long TotalBytes => (long)FileCount * BytesPerFile;
    public static TransferPlan For(TestMode mode) => mode switch
    {
        TestMode.Quick => new(mode, 1, 256 * 1024 * 1024),
        TestMode.LargeFile => new(mode, 1, 1024 * 1024 * 1024),
        TestMode.SmallFiles => new(mode, 1024, 64 * 1024),
        _ => throw new BenchmarkException(BenchmarkError.InvalidSize)
    };
}
