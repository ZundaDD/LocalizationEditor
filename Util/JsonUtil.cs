using Newtonsoft.Json;

namespace LocalizationEditor;

public static class JsonUtil
{
    public static string ToJson(this object obj, Formatting format = Formatting.None)
    => JsonConvert.SerializeObject(obj, format);

    public static T? ToObject<T>(this string json)
    => JsonConvert.DeserializeObject<T>(json);
}