using Newtonsoft.Json;

namespace LocalizationEditor;

public enum MessageType
{
    OPEN_PROJECT, CREATE_PROJECT, SAVE_PROJECT,

    PICK_LANGUAGE_FILE, PICK_LANGUAGE_FILE2, ADD_LANGUAGE, EDIT_LANGUAGE_CONFIG,

    ADD_KEY, DELETE_KEY, RENAME_KEY, EDIT_KEY,

    DIRTY_STATE, SAVE_SUCCESS,

    LOAD,
    LANG_FILE,
    LANG_FILE2,
}

public partial class MessageRouter
{
    private Dictionary<string, Func<string, TwoResult<string>>> signals = new();

    public void Register(object command, Action handler)
    {
        var key = command.ToString();
        if (key == null) return;

        signals[key] = _ =>
        {
            try
            {
                handler?.Invoke();
                return Message.T;
            }
            catch (Exception ex) { return Message.E(ex.Message); }
        };
    }

    public void Register<T>(object command, Action<T> handler)
    {
        var key = command.ToString();
        if (key == null) return;

        signals[key] = payloadString =>
        {
            try
            {
                var req = JsonConvert.DeserializeObject<T>(payloadString);

                if (req != null) handler(req);
                return Message.T;
            }
            catch (JsonException ex) { return Message.E($"Deserialization {command} failed: {ex.Message}"); }
            catch (Exception ex) { return Message.E(ex.Message); }
        };
    }

    public TwoResult<string> Handle(string fullMessage)
    {
        int separatorIndex = fullMessage.IndexOf(':');

        string command = separatorIndex == -1 ? fullMessage : fullMessage[..separatorIndex];
        string payload = separatorIndex == -1 ? string.Empty : fullMessage[(separatorIndex + 1)..];

        if (!signals.ContainsKey(command)) return Message.E("Signal not defined");

        return signals[command]?.Invoke(payload) ?? Message.E("Unknown error");
    }
}