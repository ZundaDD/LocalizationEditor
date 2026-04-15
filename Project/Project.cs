using Hjson;
using Newtonsoft.Json.Linq;

namespace LocalizationEditor;

/// <summary>
/// 项目的运行时结构
/// </summary>
public partial class Project
{
    public ProjectConfig Config { get; set; } = null!;

    public string ConfigPath { get; set; } = "";

    public Dictionary<string, ILanguageFile> Cache { get; init; } = new();

    public HashSet<string> DirtyLanguages { get; init; } = new();

    public void SaveConfig() => File.WriteAllText(ConfigPath, Config.ToJson());

    public string[] Dirty => DirtyLanguages.OrderBy(x => x).ToArray();

    public bool IsDirty => DirtyLanguages.Count > 0;

    public string ToJson()
    {
        var safeData = new Dictionary<string, JToken>();
        foreach (var kvp in Cache)
        {
            string plainJson = kvp.Value.ToJson();
            safeData[kvp.Key] = JToken.Parse(plainJson);
        }

        var payload = new
        {
            projectName = Config.ProjectName,
            mainLanguage = Config.SourceLanguage,
            languages = Config.Files.Keys,
            files = Config.Files,
            data = safeData,
            dirtyLanguages = DirtyLanguages
        };

        return payload.ToJson();
    }
}