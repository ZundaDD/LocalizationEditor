using Photino.NET;

namespace LocalizationEditor;

public static class PhotinoWindowUtil
{
    private static string previousPath = "";

    public static void Send(this PhotinoWindow window, MessageType type, string payload)
    => window.SendWebMessage($"{type}:{payload}");

    public static void Send(this PhotinoWindow window, MessageType type)
    => window.SendWebMessage(type.ToString());

    public static void Err(this PhotinoWindow window, string msg, string title = "Error occurred")
    => window.ShowMessage(title, msg);

    public static OneResult<string> ChooseOne(this PhotinoWindow window, string title = "Choose file", string defaultPath = null!, bool multiSelect = false, (string Name, string[] Extensions)[] filters = null!)
    {
        var paths = window.ShowOpenFile(title, defaultPath ?? previousPath, multiSelect, filters);
        var projPath = paths.FirstOrDefault();

        previousPath = projPath ?? "";
        return new(!string.IsNullOrEmpty(projPath), projPath ?? "");
    }

    public static OneResult<string> SaveOne(this PhotinoWindow window, string title = "Choose file", string defaultPath = null!, (string Name, string[] Extensions)[] filters = null!)
    {
        var projPath = window.ShowSaveFile(title, defaultPath ?? previousPath, filters);

        previousPath = projPath ?? "";
        return new(!string.IsNullOrEmpty(projPath), projPath ?? "");
    }
}