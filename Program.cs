using Photino.NET;
using System.Text.Json;

namespace LocalizationEditor;

public class Program
{
    private static ProjectManager proj = new();

    private static PathSelector pathSelect = new();

    private record AddLanguageRequest(string key, string path, bool isMain);
    private record EditKeyRequest(string lang, string[] path, string value);

    [STAThread]
    static void Main(string[] args)
    {
        var window = new PhotinoWindow()
            .SetTitle("Localize Editor")
            .SetUseOsDefaultSize(false)
            .SetIconFile("favicon.ico")
            .SetSize(1080, 720)
            .Load("wwwroot/index.html");

        window.RegisterWebMessageReceivedHandler((sender, message) =>
        {
            Console.WriteLine($"Photino.NET: \"{window.Title}\".ReceiveMessage({message})");
            if (message == "OPEN_PROJECT")
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
                    else window.SendWebMessage("DIRTY_STATE:" + JsonSerializer.Serialize(new { dirtyLanguages = proj.GetDirtyLanguages() }));
                }
                catch (Exception ex)
                {
                    window.ShowMessage("Error occurred", $"EDIT_KEY failed:\n{ex.Message}");
                }
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