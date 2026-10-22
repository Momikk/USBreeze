using System.Text.Json;

namespace USBreeze.Core;

public static class History
{
    public static List<BenchmarkResult> Load(string path)
    {
        if (!File.Exists(path)) return [];
        try { return JsonSerializer.Deserialize<List<BenchmarkResult>>(File.ReadAllText(path)) ?? []; }
        catch (JsonException) { return []; }
    }

    public static void Save(string path, IEnumerable<BenchmarkResult> items)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(items.Take(20), new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
