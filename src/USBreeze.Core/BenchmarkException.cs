namespace USBreeze.Core;

public enum BenchmarkError
{
    InvalidSize, Unavailable, Redirected, Network, NoSpace, Fat32, Alignment,
    Create, Write, Read, Incomplete, Flush, Seek, Cleanup
}

public sealed class BenchmarkException(BenchmarkError error, int nativeCode = 0) : IOException(error.ToString())
{
    public BenchmarkError Error { get; } = error;
    public int NativeCode { get; } = nativeCode;
}
