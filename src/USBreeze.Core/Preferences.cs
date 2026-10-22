using System.Text.Json;

namespace USBreeze.Core;

public sealed class Preferences
{
    public string Language { get; set; } = LanguageChoice.System;
    public int Mode { get; set; } = 1;

    public static Preferences Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new();
            var result = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? new();
            if (!LanguageChoice.IsValid(result.Language)) result.Language = LanguageChoice.System;
            result.Mode = Math.Clamp(result.Mode, 0, 2);
            return result;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
