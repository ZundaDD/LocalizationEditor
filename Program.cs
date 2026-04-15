using Photino.NET;
using System.Drawing;
using System.Text.Json;
using static LocalizationEditor.MessageType;

namespace LocalizationEditor;

public delegate void RequestHandler<T>(PhotinoWindow window, T request);

public partial class Program
{
    private static ProjectManager proj = new();

    private static AppState appState = AppState.Default;

    private static MessageRouter router = new();

    private static PhotinoWindow window = null!;

    [STAThread]
    static void Main(string[] args)
    {
        appState = AppStateUtil.Load();

        window = new PhotinoWindow()
            .SetTitle("Localization Editor")
            .SetUseOsDefaultSize(false)
            .SetIconFile("favicon.ico")
            .SetSize(appState.WindowWidth ?? 1080, appState.WindowHeight ?? 720)
            .SetLocation(new Point(appState.WindowLeft ?? 0, appState.WindowTop ?? 0))
            .Load("wwwroot/index.html");

        window.ContextMenuEnabled = false;
        window.DevToolsEnabled = false;
        window.SmoothScrollingEnabled = false;

        window.RegisterWindowClosingHandler((sender, args) =>
        {
            SaveWindowState(window);

            if (!proj.Opened || !proj.CurrentProj.IsDirty) return false;

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

                window.Send(DIRTY_STATE, JsonSerializer.Serialize(new { dirtyLanguages = proj.CurrentProj.Dirty }));
                window.Send(SAVE_SUCCESS);
                UpdateWindowTitle(window);
                return false;
            }

            if (result == PhotinoDialogResult.No) return false;

            return true;
        });

        router.Register(OPEN_PROJECT, SelectAndOpenProject);
        router.Register(CREATE_PROJECT, CreateProject);
        router.Register(SAVE_PROJECT, SaveProject);

        router.Register(PICK_LANGUAGE_FILE, SelectLanguageFile);
        router.Register<PickLanguageFileRequest>(PICK_LANGUAGE_FILE2, SelectLanguageFile2);
        router.Register<AddLanguageRequest>(ADD_LANGUAGE, AddLanguage);
        router.Register<EditLanguageConfigRequest>(EDIT_LANGUAGE_CONFIG, EditLanguageConfig);

        router.Register<AddKeyRequest>(ADD_KEY, AddKey);
        router.Register<EditKeyRequest>(EDIT_KEY, EditKey);
        router.Register<DeleteKeyRequest>(DELETE_KEY, DeleteKey);
        router.Register<RenameKeyRequest>(RENAME_KEY, RenameKey);

        Console.WriteLine("Signal registered");

        window.RegisterWebMessageReceivedHandler((sender, message) =>
        {
            Console.WriteLine($"Photino.NET: \"{window.Title}\".ReceiveMessage({message})");

            if (message == "APP_READY")
            {
                if (!string.IsNullOrWhiteSpace(appState.LastProjectPath) && File.Exists(appState.LastProjectPath))
                    OpenProject(window, appState.LastProjectPath);
                return;
            }

            var result = router.Handle(message);
            if (!result.success) window.Err(result.value2);
        });

        window.WaitForClose();
    }
}