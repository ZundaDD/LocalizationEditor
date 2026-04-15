using System.Runtime.CompilerServices;
using System.Text.Json;
using Photino.NET;
using Newtonsoft.Json.Linq;
using static LocalizationEditor.MessageType;

namespace LocalizationEditor;

public partial class Program
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static OneResult<string> SelectProject()
    => window.ChooseOne(title: "Open project file", filters: [("project file", ["json"])]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static OneResult<string> SelectLanguage()
    => window.ChooseOne(title: "Choose language file", filters: [("hjson file", ["hjson"])]);

    private static void SelectAndOpenProject()
    {
        var result = SelectProject();
        if (result.success) OpenProject(window, result.value);
    }

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

    private static void CreateProject()
    {
        var result = window.SaveOne(title: "Create new project", filters: [("project file", ["json"])]);
        if (!result.success) return;

        var result2 = proj.CreateProject(result.value);
        if (!result2.success)
        {
            window.ShowMessage(result2.value1, result2.value2);
            return;
        }

        OpenProject(window, result.value);
    }

    private static void SaveProject()
    {
        var result = proj.SaveProject();
        if (result.success)
        {
            window.Send(DIRTY_STATE, (new { dirtyLanguages = proj.CurrentProj.DirtyLanguages }).ToJson());
            window.Send(SAVE_SUCCESS);
            UpdateWindowTitle(window);
        }
        else window.Err(result.value2);
    }

    private static void SelectLanguageFile()
    {
        var result = SelectLanguage();
        if (result.success) window.Send(LANG_FILE, result.value);
    }

    private static void SelectLanguageFile2(PickLanguageFileRequest request)
    {
        var result = SelectLanguage();
        if (!result.success) return;

        window.SendWebMessage("LANG_FILE2:" + JsonSerializer.Serialize(new { target = request.target, path = result.value }));
    }

    private static void SendCurrentState(PhotinoWindow window)
    {
        if (!proj.Opened) return;
        window.Send(LOAD, proj.CurrentProj.ToJson());
        window.Send(DIRTY_STATE, (new { dirtyLanguages = proj.CurrentProj.DirtyLanguages }).ToJson());
    }

    private static void UpdateWindowTitle(PhotinoWindow window)
    {
        if (!proj.Opened) window.SetTitle("Localization Editor");
        else
        {
            var title = $"Localization Editor - {proj.CurrentProj.Config.ProjectName}";
            if (proj.CurrentProj.IsDirty) title += "(*)";

            window.SetTitle(title);
        }
    }

    private static void AddLanguage(AddLanguageRequest req)
    {
        var result = proj.AddLanguage(req.key, req.path, req.isMain);
        if (!result.success) window.ShowMessage(result.value1, result.value2);
        else OpenProject(window, proj.CurrentProj.ConfigPath);
    }

    private static void EditLanguageConfig(EditLanguageConfigRequest req)
    {
        var result = proj.EditLanguage(req.key, req.path, req.setMain);
        if (!result.success) window.ShowMessage(result.value1, result.value2);
        else OpenProject(window, proj.CurrentProj.ConfigPath);
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


}