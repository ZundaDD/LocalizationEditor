using System.Text.Json;
using Newtonsoft.Json;

namespace LocalizationEditor;

public sealed record AppState(
    string? LastProjectPath,
    int? WindowLeft,
    int? WindowTop,
    int? WindowWidth,
    int? WindowHeight
)
{
    public static AppState Default => new(null, null, null, null, null);
}

public static class AppStateUtil
{
    public static string GetStateFilePath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(baseDir, "LocalizationEditor");
        return Path.Combine(dir, "state.json");
    }

    public static AppState Load()
    {
        try
        {
            var path = GetStateFilePath();
            if (!File.Exists(path)) return AppState.Default;

            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<AppState>(json) ?? AppState.Default;
        }
        catch { return AppState.Default; }
    }

    public static void Save(AppState state)
    {
        var path = GetStateFilePath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var json = JsonConvert.SerializeObject(state, Formatting.Indented);
        File.WriteAllText(path, json);
    }
}

