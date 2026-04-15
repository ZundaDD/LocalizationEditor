using System.Runtime.CompilerServices;
using System.Text.Json;
using Photino.NET;
using static LocalizationEditor.MessageType;

namespace LocalizationEditor;

public partial class Program
{
    private static void AddKey(AddKeyRequest req)
    {
        var result = proj.AddKey(req.parentPath, req.key, req.kind);
        if (!result.success) window.ShowMessage(result.value1, result.value2);
        else
        {
            var path = (req.parentPath ?? Array.Empty<string>()).Append(req.key).ToArray();
            window.SendWebMessage("ADD_KEY_OK:" + (new { parentPath = req.parentPath ?? Array.Empty<string>(), key = req.key, kind = req.kind, path }).ToJson());
            window.SendWebMessage("DIRTY_STATE:" + (new { dirtyLanguages = proj.CurrentProj.DirtyLanguages }).ToJson());
            UpdateWindowTitle(window);
        }
    }

    private static void EditKey(EditKeyRequest req)
    {
        var result = proj.EditKey(req.lang, req.path, req.value);
        if (!result.success) window.ShowMessage(result.value1, result.value2);
        else
        {
            window.SendWebMessage("DIRTY_STATE:" + (new { dirtyLanguages = proj.CurrentProj.DirtyLanguages }).ToJson());
            UpdateWindowTitle(window);
        }
    }

    private static void DeleteKey(DeleteKeyRequest req)
    {
        var result = proj.DeleteKey(req.path);
        if (!result.success) window.ShowMessage(result.value1, result.value2);
        else
        {
            window.SendWebMessage("DELETE_KEY_OK:" + JsonSerializer.Serialize(new { path = req.path }));
            window.SendWebMessage("DIRTY_STATE:" + (new { dirtyLanguages = proj.CurrentProj.DirtyLanguages }).ToJson());
            UpdateWindowTitle(window);
        }
    }

    private static void RenameKey(RenameKeyRequest req)
    {
        var result = proj.RenameKey(req.path, req.newKey);
        if (!result.success) window.ShowMessage(result.value1, result.value2);
        else
        {
            var newPath = req.path.Take(req.path.Length - 1).Append(req.newKey).ToArray();
            window.SendWebMessage("RENAME_KEY_OK:" + JsonSerializer.Serialize(new { oldPath = req.path, newPath }));
            window.SendWebMessage("DIRTY_STATE:" + (new { dirtyLanguages = proj.CurrentProj.DirtyLanguages }).ToJson());
            UpdateWindowTitle(window);
        }
    }
}