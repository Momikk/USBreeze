using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace USBreeze.App;

public sealed class HelpWindow : Window
{
    public HelpWindow()
    {
        var text = Texts.Current;
        Title = text["Help"];
        Width = 680; Height = 500; MinWidth = 480; MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(245, 247, 251));
        FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI");
        var tabs = new TabControl { Margin = new Thickness(18) };
        var version = Assembly.GetExecutingAssembly().GetName().Version!;
        var versionLabel = $"{version.Major}.{version.Minor}.{version.Build}";
        AddTab(tabs, text["Help"], $"USBreeze v{versionLabel}\n\n{text["HelpText"]}\n\n{text["SourceCode"]}\nhttps://github.com/Momikk/USBreeze/tree/v{versionLabel}");
        AddTab(tabs, "AGPL-3.0", Resource("USBreeze.LICENSE"));
        AddTab(tabs, text["ThirdParty"], Resource("USBreeze.DOTNET-LICENSE") + "\n\n" + Resource("USBreeze.DOTNET-NOTICES"));
        Content = tabs;
    }

    private static void AddTab(TabControl tabs, string title, string content) => tabs.Items.Add(new TabItem
    {
        Header = title,
        Content = new TextBox
        {
            Text = content, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(16), FontSize = 13, BorderThickness = new Thickness(0)
        }
    });

    private static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing bundled license: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
