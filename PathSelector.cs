using Photino.NET;

namespace LocalizationEditor;

public class PathSelector
{
    private string previousPath = "";

    public OneResult<string> ChooseOne(PhotinoWindow window, string title = "Choose file", string defaultPath = null!, bool multiSelect = false, (string Name, string[] Extensions)[] filters = null!)
    {
        var paths = window.ShowOpenFile(title, defaultPath ?? previousPath, multiSelect, filters);
        var projPath = paths.FirstOrDefault();

        previousPath = projPath ?? "";
        return new(!string.IsNullOrEmpty(projPath), projPath ?? "");
    }

    public OneResult<string> SaveOne(PhotinoWindow window, string title = "Choose file", string defaultPath = null!, (string Name, string[] Extensions)[] filters = null!)
    {
        var projPath = window.ShowSaveFile(title, defaultPath ?? previousPath, filters);

        previousPath = projPath ?? "";
        return new(!string.IsNullOrEmpty(projPath), projPath ?? "");
    }
}