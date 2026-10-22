using System.Text;

namespace USBreeze.Core;

public sealed record UsbDrive(string Root, string Name, string Format, long TotalBytes, long FreeBytes, bool ConfirmedUsb)
{
    public string DisplayName => $"{Name}  ·  {Root[..2]}";
    public override string ToString() => DisplayName;
}

public static class Devices
{
    public static IReadOnlyList<UsbDrive> Discover()
    {
        var systemRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        var result = new List<UsbDrive>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.Name.Equals(systemRoot, StringComparison.OrdinalIgnoreCase) ||
                    drive.DriveType is not (DriveType.Fixed or DriveType.Removable)) continue;
                var (isUsb, model) = Probe(drive.Name);
                if (!isUsb && drive.DriveType != DriveType.Removable) continue;
                var label = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? model : drive.VolumeLabel;
                if (string.IsNullOrWhiteSpace(label)) label = "USB";
                result.Add(new(drive.Name, label, drive.DriveFormat, drive.TotalSize, drive.AvailableFreeSpace, isUsb));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* Unplugged / locked. */ }
        }
        return result;
    }

    private static (bool, string) Probe(string root)
    {
        // Metadata query only: no raw reads, raw writes or administrator access.
        using var handle = Native.CreateFile(@"\\.\" + root[..2], 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle.IsInvalid) return (false, "");
        var output = new byte[4096];
        if (!Native.DeviceIoControl(handle, 0x002D1400, new byte[12], 12, output, output.Length, out var returned, IntPtr.Zero) || returned < 36)
            return (false, "");
        var offset = BitConverter.ToInt32(output, 16);
        var model = "";
        if (offset > 0 && offset < returned)
        {
            var end = Array.IndexOf(output, (byte)0, offset, returned - offset);
            model = Encoding.ASCII.GetString(output, offset, (end < 0 ? returned : end) - offset).Trim();
        }
        return (BitConverter.ToInt32(output, 28) == 7, model);
    }
}
