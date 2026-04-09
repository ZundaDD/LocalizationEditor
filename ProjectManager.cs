using System.Text.Json;
using System.Text.RegularExpressions;
using Hjson;
using Newtonsoft.Json;

namespace LocalizationEditor;

public class ProjectManager
{
    public ProjectConfig Config { get; private set; } = null!;

    public string ProjectFilePath { get; private set; } = "";

    private HjsonConverter converter = new();

    public Dictionary<string, JsonValue> Cache { get; private set; } = new();

    private readonly HashSet<string> dirtyLanguages = new();

    public string[] GetDirtyLanguages() => dirtyLanguages.OrderBy(x => x).ToArray();

    public bool IsDirty => dirtyLanguages.Count > 0;


    public TwoResult<string> OpenProject(string filePath)
    {
        try
        {
            string projJson = File.ReadAllText(filePath);

            var projConfig = JsonConvert.DeserializeObject<ProjectConfig>(projJson);
            if (projConfig == null || !projConfig.IsL10nProject)
                return new(false, "Error occurred", "Invalid file content");

            Cache = new();
            Config = projConfig;
            ProjectFilePath = filePath;
            dirtyLanguages.Clear();

            //加载所有的语言文件
            foreach (var lang in projConfig.Files)
            {
                string langName = lang.Key;
                string path = lang.Value;

                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                var jsonValue = converter.LoadFrom(path);
                Cache[langName] = jsonValue;
            }

            return new(true, "", "");
        }
        catch (Newtonsoft.Json.JsonException)
        {
            return new(false, "Error occurred", "Invalid Json format, please check file content.");
        }
        catch (Exception ex)
        {
            return new(false, "Error occurred", $"Unknown exception:\n{ex.Message}");
        }
    }

    public TwoResult<string> CreateProject(string filePath)
    {
        try
        {
            string projectName = Path.GetFileNameWithoutExtension(filePath);

            var newProject = new ProjectConfig { ProjectName = projectName, IsL10nProject = true };
            string jsonContent = JsonConvert.SerializeObject(newProject, Formatting.Indented);
            File.WriteAllText(filePath, jsonContent);

            return new(true, "", "");
        }
        catch (Exception ex)
        {
            return new(false, "Error occurred", $"Unknown exception:\n{ex.Message}");
        }
    }

    public TwoResult<string> AddLanguage(string key, string path, bool isMain)
    {
        try
        {
            if (Config == null || string.IsNullOrWhiteSpace(ProjectFilePath))
                return new(false, "Error occurred", "Project not loaded.");

            key = key.Trim(); path = path.Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(path))
                return new(false, "Error occurred", "Language key/path cannot be empty.");

            if (Config.Files.Count == 0) isMain = true;
            if (isMain) Config.SourceLanguage = key;

            if (!File.Exists(path)) return new(false, "Error occurred", $"Language file not found:\n{path}");

            Config.Files[key] = path;
            Cache[key] = converter.LoadFrom(path);

            SaveConfig();

            return new(true, "", "");
        }
        catch (Exception ex)
        {
            return new(false, "Error occurred", $"Unknown exception:\n{ex.Message}");
        }
    }

    public TwoResult<string> SaveProject()
    {
        try
        {
            if (Config == null || string.IsNullOrWhiteSpace(ProjectFilePath))
                return new(false, "Error occurred", "Project not loaded.");

            foreach (var kvp in Config.Files)
            {
                if (!dirtyLanguages.Contains(kvp.Key)) continue;
                if (string.IsNullOrWhiteSpace(kvp.Value)) continue;
                if (!Cache.ContainsKey(kvp.Key)) continue;
                if (!File.Exists(kvp.Value)) continue;

                converter.SaveTo(Cache[kvp.Key].ToString(), kvp.Value);
            }

            SaveConfig();
            dirtyLanguages.Clear();
            return new(true, "", "");
        }
        catch (Exception ex)
        {
            return new(false, "Error occurred", $"Unknown exception:\n{ex.Message}");
        }
    }

    public TwoResult<string> EditLanguage(string lang, string[] path, string value)
    {
        if (!Cache.ContainsKey(lang)) return Message.E($"lang {lang} not loaded");
        if (path == null || path.Length == 0) return Message.E("path invalid");

        JsonObject current = Cache[lang].Qo();
        if (current == null) return Message.E("json convert failed");

        for (int i = 0; i < path.Length - 1; i++)
        {
            string key = path[i];

            if (!current.ContainsKey(key) || current[key] == null || current[key].JsonType != JsonType.Object)
            {
                current[key] = new JsonObject();
            }

            current = current[key].Qo();
        }

        string finalKey = path[^1];
        current[finalKey] = value;

        dirtyLanguages.Add(lang);
        return Message.T;
    }

    private void SaveConfig()
    {
        var jsonContent = JsonConvert.SerializeObject(Config, Formatting.Indented);
        File.WriteAllText(ProjectFilePath, jsonContent);
    }
}