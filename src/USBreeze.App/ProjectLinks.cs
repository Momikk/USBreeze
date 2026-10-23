using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace USBreeze.App;

internal static class ProjectLinks
{
    internal const string Homepage = "https://github.com/Momikk";
    internal static void OpenHomepage(Window owner)
    {
        try { Process.Start(new ProcessStartInfo(Homepage) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        { MessageBox.Show(owner, Homepage, "Momikk", MessageBoxButton.OK, MessageBoxImage.Information); }
    }
}
