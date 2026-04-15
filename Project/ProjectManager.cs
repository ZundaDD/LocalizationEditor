using Hjson;
using Newtonsoft.Json;

namespace LocalizationEditor;

public class ProjectManager
{
    public Project CurrentProj { get; private set; } = null!;

    public bool Opened => CurrentProj != null;

    public TwoResult<string> OpenProject(string filePath)
    {
        string projJson = File.ReadAllText(filePath);

        var projConfig = projJson.ToObject<ProjectConfig>();
        if (projConfig == null || !projConfig.IsL10nProject) return Message.E("Invalid file content");

        CurrentProj = new() { Config = projConfig, ConfigPath = filePath };

        foreach (var lang in projConfig.Files)
        {
            string langName = lang.Key;
            string path = lang.Value;

            if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

            var language = LanguageUtil.Load(path);
            CurrentProj.Cache[langName] = language;
        }

        return Message.T;
    }

    public TwoResult<string> CreateProject(string filePath)
    {
        string projectName = Path.GetFileNameWithoutExtension(filePath);

        var newProject = new ProjectConfig { ProjectName = projectName, IsL10nProject = true };
        File.WriteAllText(filePath, newProject.ToJson());

        return Message.T;
    }

    public TwoResult<string> AddLanguage(string key, string path, bool isMain)
    {
        if (CurrentProj == null) return Message.E("Project not loaded.");

        key = key.Trim(); path = path.Trim();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(path))
            return Message.E("Language key/path cannot be empty.");

        if (CurrentProj.Config.Files.Count == 0) isMain = true;
        if (isMain) CurrentProj.Config.SourceLanguage = key;

        if (!File.Exists(path)) return Message.E($"Language file not found:\n{path}");

        CurrentProj.Config.Files[key] = path;
        CurrentProj.Cache[key] = LanguageUtil.Load(path);
        CurrentProj.SaveConfig();

        return Message.T;
    }

    public TwoResult<string> EditLanguage(string key, string path, bool setMain)
    {
        if (CurrentProj == null) return Message.E("Project not loaded.");

        key = key.Trim();
        path = path.Trim();

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(path))
            return Message.E("Language key/path cannot be empty.");

        if (!CurrentProj.Config.Files.ContainsKey(key))
            return Message.E($"Language not registered: {key}");

        if (!File.Exists(path))
            return Message.E($"Language file not found:\n{path}");

        CurrentProj.Config.Files[key] = path;
        if (setMain) CurrentProj.Config.SourceLanguage = key;

        CurrentProj.Cache[key] = LanguageUtil.Load(path);
        CurrentProj.SaveConfig();

        return Message.T;
    }

    public TwoResult<string> SaveProject()
    {
        if (CurrentProj == null) return Message.E("Project not loaded.");

        foreach (var kvp in CurrentProj.Config.Files)
        {
            if (!CurrentProj.DirtyLanguages.Contains(kvp.Key)) continue;
            if (string.IsNullOrWhiteSpace(kvp.Value)) continue;
            if (!CurrentProj.Cache.ContainsKey(kvp.Key)) continue;
            if (!File.Exists(kvp.Value)) continue;

            CurrentProj.Cache[kvp.Key].Save();
        }

        CurrentProj.SaveConfig();
        CurrentProj.DirtyLanguages.Clear();
        return Message.T;
    }

    public TwoResult<string> EditKey(string lang, string[] path, string value)
    {
        if (CurrentProj == null) return Message.E("Project not loaded");
        if (!CurrentProj.Cache.ContainsKey(lang)) return Message.E($"lang {lang} not loaded");
        if (path == null || path.Length == 0) return Message.E("path invalid");

        CurrentProj.Cache[lang].EditKey(path.Take(path.Length - 1).ToArray(), path[^1], value);
        CurrentProj.DirtyLanguages.Add(lang);
        return Message.T;
    }

    public TwoResult<string> AddKey(string[] parentPath, string key, string kind)
    {
        if (CurrentProj == null) return Message.E("Project not loaded");
        if (string.IsNullOrWhiteSpace(key)) return Message.E("key invalid");

        parentPath ??= Array.Empty<string>();
        key = key.Trim();
        kind = (kind ?? "").Trim().ToLowerInvariant();
        if (kind != "object" && kind != "string") return Message.E("kind invalid (object|string)");

        foreach (var lang in CurrentProj.Config.Files.Keys)
        {
            if (!CurrentProj.Cache.TryGetValue(lang, out var root) || root == null) continue;
            var contain = root.ContainsKey(parentPath, key);

            //没有key是被允许的，因为其他语言是对主语言的补丁
            if (contain) return Message.E($"Key dup: {lang}:{string.Join('.', parentPath.Append(key))}");
        }

        foreach (var lang in CurrentProj.Config.Files.Keys)
        {
            if (!CurrentProj.Cache.TryGetValue(lang, out var root) || root == null)
                return Message.E($"Language file null: {lang}");

            root.AddKey(parentPath, key, kind);
            CurrentProj.DirtyLanguages.Add(lang);
        }

        return Message.T;
    }

    public TwoResult<string> DeleteKey(string[] path)
    {
        if (CurrentProj == null) return Message.E("Project not loaded.");
        if (path == null || path.Length == 0) return Message.E("path invalid");

        var parentPath = path.Take(path.Length - 1).ToArray();
        var key = path[^1];

        foreach (var lang in CurrentProj.Config.Files.Keys)
        {
            if (!CurrentProj.Cache.TryGetValue(lang, out var root) || root == null)
                continue;

            if (root.DeleteKey(parentPath, key))
                CurrentProj.DirtyLanguages.Add(lang);
        }

        return Message.T;
    }

    public TwoResult<string> RenameKey(string[] path, string newKey)
    {
        if (CurrentProj == null) return Message.E("Project not loaded.");
        if (path == null || path.Length == 0) return Message.E("path invalid");
        if (string.IsNullOrWhiteSpace(newKey)) return Message.E("newKey invalid");

        newKey = newKey.Trim();

        var parentPath = path.Take(path.Length - 1).ToArray();
        var oldKey = path[^1];
        if (oldKey == newKey) return Message.T;

        foreach (var lang in CurrentProj.Config.Files.Keys)
        {
            if (!CurrentProj.Cache.TryGetValue(lang, out var root) || root == null)
                continue;
            if (!root.ContainsKey(parentPath, oldKey)) continue;

            if (root.ContainsKey(parentPath, newKey))
                return Message.E($"Key dup: {lang}:{string.Join('.', parentPath.Append(newKey))}");
        }

        foreach (var lang in CurrentProj.Config.Files.Keys)
        {
            if (!CurrentProj.Cache.TryGetValue(lang, out var root) || root == null) continue;
            if (root.RenameKey(parentPath, oldKey, newKey))
                CurrentProj.DirtyLanguages.Add(lang);
        }

        return Message.T;
    }

}