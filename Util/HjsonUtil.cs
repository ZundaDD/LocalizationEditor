using Hjson;

namespace LocalizationEditor;

public static class HjsonUtil
{
    public static JsonObject GetOrCreate(this JsonObject root, IEnumerable<string> path)
    {
        var current = root ?? new JsonObject();
        foreach (var key in path)
        {
            if (!current.ContainsKey(key) || current[key] == null || current[key].JsonType != JsonType.Object)
                current[key] = new WscJsonObject();
            current = current[key].Qo();
        }
        return current;
    }

    public static JsonObject? GetOrNull(this JsonObject root, IEnumerable<string> path)
    {
        var current = root;
        foreach (var key in path)
        {
            if (current == null) return null;
            if (!current.ContainsKey(key) || current[key] == null || current[key].JsonType != JsonType.Object) return null;
            current = current[key].Qo();
        }
        return current;
    }

    public static JsonValue SaveTo(string json, string path)
    {
        var parseOptions = new HjsonOptions { KeepWsc = true };
        var hjsonVal = HjsonValue.Parse(json, parseOptions);

        var writeOptions = new HjsonOptions { EmitRootBraces = false, };

        var str = hjsonVal.ToString(writeOptions);

        File.WriteAllText(path, str);
        return hjsonVal;
    }

    public static JsonValue LoadFrom(string path) => HjsonValue.Load(path);
}