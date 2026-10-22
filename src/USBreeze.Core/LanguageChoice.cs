using System.Globalization;
using System.Runtime.InteropServices;

namespace USBreeze.Core;

public static class LanguageChoice
{
    public const string System = "system";
    public const string English = "en";
    public const string SimplifiedChinese = "zh-Hans";
    public static bool IsValid(string? value) => value is System or English or SimplifiedChinese;

    public static string Resolve(string? choice, CultureInfo osDisplayLanguage)
    {
        if (choice is English or SimplifiedChinese) return choice;
        // Match supported culture families. Traditional Chinese is NOT Simplified Chinese.
        for (var culture = osDisplayLanguage; !culture.Equals(CultureInfo.InvariantCulture); culture = culture.Parent)
        {
            if (culture.Name.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase)) return SimplifiedChinese;
            if (culture.Name.Equals("en", StringComparison.OrdinalIgnoreCase)) return English;
        }
        return English;
    }

    public static CultureInfo GetOsDisplayLanguage()
    {
        try { return CultureInfo.GetCultureInfo(GetUserDefaultUILanguage()); }
        catch (CultureNotFoundException) { return CultureInfo.GetCultureInfo("en"); }
    }

    [DllImport("kernel32.dll")]
    private static extern ushort GetUserDefaultUILanguage();
}
