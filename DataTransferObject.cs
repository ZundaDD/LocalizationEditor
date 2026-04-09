namespace LocalizationEditor;

public record class OneResult<T>(bool success, T value);

public record class TwoResult<T>(bool success, T value1, T value2);

public static class Message
{
    public static TwoResult<string> T => new(true, "", "");

    public static TwoResult<string> E(string msg, string title = "Error occurred")
    => new(false, title, msg);
}