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

    public TwoResult<string> EditLanguageConfig(string key, string path, bool setMain)
    {
        try
        {
            if (Config == null || string.IsNullOrWhiteSpace(ProjectFilePath))
                return new(false, "Error occurred", "Project not loaded.");

            key = (key ?? "").Trim();
            path = (path ?? "").Trim();

            if (string.IsNullOrWhiteSpace(key))
                return new(false, "Error occurred", "Language key cannot be empty.");
            if (string.IsNullOrWhiteSpace(path))
                return new(false, "Error occurred", "Language path cannot be empty.");

            if (!Config.Files.ContainsKey(key))
                return new(false, "Error occurred", $"Language not registered: {key}");

            if (!File.Exists(path))
                return new(false, "Error occurred", $"Language file not found:\n{path}");

            // 更新配置（路径/主语言）
            Config.Files[key] = path;
            if (setMain) Config.SourceLanguage = key;

            // 重新加载该语言缓存（避免继续使用旧文件内容）
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

    public TwoResult<string> AddKey(string[] parentPath, string key, string kind)
    {
        try
        {
            if (Config == null) return Message.E("Project not loaded.");
            if (string.IsNullOrWhiteSpace(key)) return Message.E("key invalid");

            parentPath ??= Array.Empty<string>();
            key = key.Trim();
            kind = (kind ?? "").Trim().ToLowerInvariant();
            if (kind != "object" && kind != "string") return Message.E("kind invalid (object|string)");

            foreach (var lang in Config.Files.Keys)
            {
                if (!Cache.TryGetValue(lang, out var root) || root == null || root.JsonType != JsonType.Object) continue;

                var parentObj = root.Qo().GetOrNull(parentPath);
                //没有key是被允许的，因为其他语言是对主语言的补丁
                if (parentObj != null && parentObj.ContainsKey(key))
                    return Message.E($"key already exists: {lang}:{string.Join('.', parentPath.Append(key))}");
            }

            foreach (var lang in Config.Files.Keys)
            {
                if (!Cache.TryGetValue(lang, out var root) || root == null || root.JsonType != JsonType.Object)
                {
                    root = new WscJsonObject();
                    Cache[lang] = root;
                }

                var parentObj = root.Qo().GetOrCreate(parentPath);

                if (kind == "object") parentObj[key] = new WscJsonObject();
                else parentObj[key] = "";
                dirtyLanguages.Add(lang);
            }

            return Message.T;
        }
        catch (Exception ex)
        {
            return Message.E(ex.ToString());
        }
    }

    public TwoResult<string> DeleteKey(string[] path)
    {
        if (Config == null) return Message.E("Project not loaded.");
        if (path == null || path.Length == 0) return Message.E("path invalid");

        var parentPath = path.Take(path.Length - 1).ToArray();
        var key = path[^1];

        foreach (var lang in Config.Files.Keys)
        {
            if (!Cache.TryGetValue(lang, out var root) || root == null || root.JsonType != JsonType.Object) continue;

            var parentObj = root.Qo().GetOrNull(parentPath);
            //                          在此移除  ↓
            if (parentObj != null && parentObj.Remove(key)) dirtyLanguages.Add(lang);
        }

        return Message.T;
    }

    public TwoResult<string> RenameKey(string[] path, string newKey)
    {
        if (Config == null) return Message.E("Project not loaded.");
        if (path == null || path.Length == 0) return Message.E("path invalid");
        if (string.IsNullOrWhiteSpace(newKey)) return Message.E("newKey invalid");

        newKey = newKey.Trim();

        var parentPath = path.Take(path.Length - 1).ToArray();
        var oldKey = path[^1];
        if (oldKey == newKey) return Message.T;

        foreach (var lang in Config.Files.Keys)
        {
            if (!Cache.TryGetValue(lang, out var root) || root == null || root.JsonType != JsonType.Object) continue;

            var parentObj = root.Qo().GetOrNull(parentPath);
            if (parentObj == null) continue;
            if (!parentObj.ContainsKey(oldKey)) continue;

            if (parentObj.ContainsKey(newKey)) return Message.E($"rename conflict: {lang}:{string.Join('.', parentPath.Append(newKey))}");
        }

        foreach (var lang in Config.Files.Keys)
        {

            if (!Cache.TryGetValue(lang, out var root) || root == null || root.JsonType != JsonType.Object) continue;

            var parentObj = root.Qo().GetOrNull(parentPath);
            if (parentObj == null) continue;
            if (!parentObj.ContainsKey(oldKey)) continue;

            var ordered = parentObj.ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Key == oldKey)
                {
                    ordered[i] = new KeyValuePair<string, JsonValue>(newKey, ordered[i].Value);
                    break;
                }
            }

            parentObj.Clear();
            foreach (var kv in ordered) parentObj.Add(kv.Key, kv.Value);

            dirtyLanguages.Add(lang);
        }

        return Message.T;
    }

    private void SaveConfig()
    {
        var jsonContent = JsonConvert.SerializeObject(Config, Formatting.Indented);
        File.WriteAllText(ProjectFilePath, jsonContent);
    }
}