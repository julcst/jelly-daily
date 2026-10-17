using System.Text.Json;
namespace JellyDaily;

public sealed class StateStore(string path)
{
    public Dictionary<string, ReleaseState> Load()
    {
        if (!File.Exists(path)) return [];
        // Corrupt state must stop access, never silently grant a fresh release.
        return JsonSerializer.Deserialize<Dictionary<string, ReleaseState>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Daily Episodes state is empty.");
    }
    public void Save(Dictionary<string, ReleaseState> state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(file, state);
            file.Flush(true);
        }
        File.Move(temp, path, true);
    }
}
