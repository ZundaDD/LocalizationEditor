namespace LocalizationEditor;

public record class OneResult<T>(bool success, T value);

public record class TwoResult<T>(bool success, T value1, T value2);

public static class Message
{
    public static TwoResult<string> T => new(true, "", "");

    public static TwoResult<string> E(string msg, string title = "Error occurred")
    => new(false, title, msg);
}

public record AddLanguageRequest(string key, string path, bool isMain);

public record PickLanguageFileRequest(string target);

public record EditLanguageConfigRequest(string key, string path, bool setMain);

public record EditKeyRequest(string lang, string[] path, string value);

public record AddKeyRequest(string[] parentPath, string key, string kind);

public record DeleteKeyRequest(string[] path);

public record RenameKeyRequest(string[] path, string newKey);
