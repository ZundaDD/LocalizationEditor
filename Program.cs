using Photino.NET;
using System.Drawing;
using System.Text.Json;

namespace LocalizationEditor;

public class Program
{
    private static ProjectManager proj = new();

    private static PathSelector pathSelect = new();

    private static AppState appState = AppState.Default;


    [STAThread]
    static void Main(string[] args)
    {
        appState = AppStateUtil.Load();

        var window = new PhotinoWindow()
            .SetTitle("Localization Editor")
            .SetUseOsDefaultSize(false)
            .SetIconFile("favicon.ico")
            .SetSize(appState.WindowWidth ?? 1080, appState.WindowHeight ?? 720)
            .Load("wwwroot/index.html");
        window.ContextMenuEnabled = false;

        window.RegisterWindowClosingHandler((sender, args) =>
        {
            SaveWindowState(window);

            if (proj.Config == null || !proj.IsDirty) return false;

            var result = window.ShowMessage(
                "保存",
                "项目存在未保存更改，是否保存并退出",
                PhotinoDialogButtons.YesNoCancel,
                PhotinoDialogIcon.Question);

            if (result == PhotinoDialogResult.Yes)
            {
                var saveResult = proj.SaveProject();
                if (!saveResult.success)
                {
                    window.ShowMessage(saveResult.value1, saveResult.value2);
                    return true;
                }

                window.SendWebMessage("DIRTY_STATE:" + JsonSerializer.Serialize(new { dirtyLanguages = proj.GetDirtyLanguages() }));
                window.SendWebMessage("SAVE_SUCCESS");
                UpdateWindowTitle(window);
                return false;
            }

            if (result == PhotinoDialogResult.No) return false;

            return true;
        });

        window.RegisterWebMessageReceivedHandler((sender, message) =>
        {
            Console.WriteLine($"Photino.NET: \"{window.Title}\".ReceiveMessage({message})");

            if (message == "APP_READY")
            {
                if (!string.IsNullOrWhiteSpace(appState.LastProjectPath) && File.Exists(appState.LastProjectPath))
                    OpenProject(window, appState.LastProjectPath);

                if (appState.WindowLeft.HasValue && appState.WindowTop.HasValue)
                    window.SetLocation(new Point(appState.WindowLeft.Value, appState.WindowTop.Value));
            }
            else if (message == "OPEN_PROJECT")
            {
                var result = SelectProject(window);
                if (result.success) OpenProject(window, result.value);
            }
            else if (message == "CREATE_PROJECT") CreateProject(window);
            else if (message == "PICK_LANGUAGE_FILE")
            {
                var result = pathSelect.ChooseOne(window, title: "Choose language file", filters: [("hjson file", ["hjson"])]);
                if (result.success) window.SendWebMessage("LANG_FILE:" + result.value);
            }
            else if (message == "SAVE_PROJECT")
            {
                var result = proj.SaveProject();
                if (result.success)
                {
                    window.SendWebMessage("DIRTY_STATE:" + JsonSerializer.Serialize(new { dirtyLanguages = proj.GetDirtyLanguages() }));
                    window.SendWebMessage("SAVE_SUCCESS");
                    UpdateWindowTitle(window);
                }
                else window.ShowMessage(result.value1, result.value2);

            }
            else if (message.StartsWith("ADD_LANGUAGE:"))
            {
                if (proj.Config == null) return;

                try
                {
                    var json = message["ADD_LANGUAGE:".Length..];
                    var req = JsonSerializer.Deserialize<AddLanguageRequest>(json);
                    if (req == null)
                    {
                        window.ShowMessage("Error occurred", "Invalid request payload.");
                        return;
                    }

                    var result = proj.AddLanguage(req.key, req.path, req.isMain);
                    if (!result.success) window.ShowMessage(result.value1, result.value2);
                    else OpenProject(window, proj.ProjectFilePath);
                }
                catch (Exception ex)
                {
                    window.ShowMessage("Error occurred", $"ADD_LANGUAGE failed:\n{ex.Message}");
                }
            }
            else if (message.StartsWith("EDIT_KEY:"))
            {
                try
                {
                    var json = message["EDIT_KEY:".Length..];
                    var req = JsonSerializer.Deserialize<EditKeyRequest>(json);
                    if (req == null) return;

                    var result = proj.EditLanguage(req.lang, req.path, req.value);
                    if (!result.success) window.ShowMessage(result.value1, result.value2);
                    else
                    {
                        window.SendWebMessage("DIRTY_STATE:" + JsonSerializer.Serialize(new { dirtyLanguages = proj.GetDirtyLanguages() }));
                        UpdateWindowTitle(window);
                    }
                }
                catch (Exception ex)
                {
                    window.ShowMessage("Error occurred", $"EDIT_KEY failed:\n{ex.Message}");
                }
            }
            else if (message.StartsWith("ADD_KEY:"))
            {
                try
                {
                    if (proj.Config == null) return;
                    var json = message["ADD_KEY:".Length..];
                    var req = JsonSerializer.Deserialize<AddKeyRequest>(json);
                    if (req == null) return;

                    var result = proj.AddKey(req.parentPath, req.key, req.kind);
                    if (!result.success) window.ShowMessage(result.value1, result.value2);
                    else
                    {
                        window.SendWebMessage("DIRTY_STATE:" + JsonSerializer.Serialize(new { dirtyLanguages = proj.GetDirtyLanguages() }));
                        UpdateWindowTitle(window);
                    }
                }
                catch (Exception ex)
                {
                    window.ShowMessage("Error occurred", $"ADD_KEY failed:\n{ex.Message}");
                }
            }
            else if (message.StartsWith("DELETE_KEY:"))
            {
                try
                {
                    if (proj.Config == null) return;
                    var json = message["DELETE_KEY:".Length..];
                    var req = JsonSerializer.Deserialize<DeleteKeyRequest>(json);
                    if (req == null) return;

                    var result = proj.DeleteKey(req.path);
                    if (!result.success) window.ShowMessage(result.value1, result.value2);
                    else
                    {
                        window.SendWebMessage("DIRTY_STATE:" + JsonSerializer.Serialize(new { dirtyLanguages = proj.GetDirtyLanguages() }));
                        UpdateWindowTitle(window);
                    }
                }
                catch (Exception ex) { window.ShowMessage("Error occurred", $"DELETE_KEY failed:\n{ex.Message}"); }
            }
            else if (message.StartsWith("RENAME_KEY:"))
            {
                try
                {
                    if (proj.Config == null) return;
                    var json = message["RENAME_KEY:".Length..];
                    var req = JsonSerializer.Deserialize<RenameKeyRequest>(json);
                    if (req == null) return;

                    var result = proj.RenameKey(req.path, req.newKey);
                    if (!result.success) window.ShowMessage(result.value1, result.value2);
                    else
                    {
                        window.SendWebMessage("DIRTY_STATE:" + JsonSerializer.Serialize(new { dirtyLanguages = proj.GetDirtyLanguages() }));
                        UpdateWindowTitle(window);
                    }
                }
                catch (Exception ex) { window.ShowMessage("Error occurred", $"RENAME_KEY failed:\n{ex.Message}"); }
            }
        });

        window.WaitForClose();
    }

    private static OneResult<string> SelectProject(PhotinoWindow window)
    => pathSelect.ChooseOne(window, title: "Open project file", filters: [("project file", ["json"])]);

    private static void OpenProject(PhotinoWindow window, string path)
    {
        var result = proj.OpenProject(path);
        if (!result.success)
        {
            window.ShowMessage(result.value1, result.value2);
            return;
        }

        appState = appState with { LastProjectPath = path };
        AppStateUtil.Save(appState);
        UpdateWindowTitle(window);
        SendCurrentState(window);
    }

    private static void SendCurrentState(PhotinoWindow window)
    {
        if (proj.Config == null) return;

        var safeData = new Dictionary<string, JsonElement>();
        foreach (var kvp in proj.Cache)
        {
            string plainJson = kvp.Value.ToString(Hjson.Stringify.Plain);
            safeData[kvp.Key] = JsonSerializer.Deserialize<JsonElement>(plainJson);
        }

        var payload = new
        {
            projectName = proj.Config.ProjectName,
            mainLanguage = proj.Config.SourceLanguage,
            languages = proj.Config.Files.Keys,
            data = safeData,
            dirtyLanguages = proj.GetDirtyLanguages()
        };

        window.SendWebMessage("LOAD:" + JsonSerializer.Serialize(payload));
        window.SendWebMessage("DIRTY_STATE:" + JsonSerializer.Serialize(new { dirtyLanguages = proj.GetDirtyLanguages() }));
    }

    private static void UpdateWindowTitle(PhotinoWindow window)
    {
        if (proj.Config == null)
        {
            window.SetTitle("Localization Editor");
            return;
        }

        var title = $"Localization Editor - {proj.Config.ProjectName}";
        if (proj.IsDirty) title += "(*)";
        window.SetTitle(title);
    }

    private static void SaveWindowState(PhotinoWindow window)
    {
        try
        {
            appState = appState with
            {
                WindowLeft = window.Left,
                WindowTop = window.Top,
                WindowWidth = window.Width,
                WindowHeight = window.Height
            };
            AppStateUtil.Save(appState);
        }
        catch { }
    }

    private static void CreateProject(PhotinoWindow window)
    {
        var result = pathSelect.SaveOne(window, title: "Create new project", filters: [("project file", ["json"])]);
        if (!result.success) return;

        var result2 = proj.CreateProject(result.value);
        if (!result2.success)
        {
            window.ShowMessage(result2.value1, result2.value2);
            return;
        }

        OpenProject(window, result.value);
    }
}