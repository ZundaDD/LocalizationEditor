using Hjson;

namespace LocalizationEditor;

public partial class HjsonFile : ILanguageFile
{
    private JsonValue value = null!;

    public string FilePath { get; set; }

    public bool Load()
    {
        value = HjsonUtil.LoadFrom(FilePath);
        return value != null;
    }

    public void Save()
    => HjsonUtil.SaveTo(value.ToString(), FilePath);

    public string ToJson() => value.ToString(Hjson.Stringify.Plain);
    public bool ContainsKey(string[] parentPath, string key)
    {
        var parentObj = value.Qo().GetOrNull(parentPath);
        if (parentObj == null) return false;

        return parentObj.ContainsKey(key);
    }

    public bool RenameKey(string[] parentPath, string oldKey, string newKey)
    {
        if (value == null || value.JsonType != JsonType.Object) return false;

        var parentObj = value.Qo().GetOrNull(parentPath);
        if (parentObj == null) return false;
        if (!parentObj.ContainsKey(oldKey)) return false;

        //TODO:优化
        var ordered = parentObj.ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].Key == oldKey)
            {
                ordered[i] = new KeyValuePair<string, JsonValue>(newKey, ordered[i].Value);
                break;
            }
        }

        parentObj.Clear();
        foreach (var kv in ordered) parentObj.Add(kv.Key, kv.Value);

        return true;
    }

    public bool DeleteKey(string[] parentPath, string key)
    {
        var parentObj = value.Qo().GetOrNull(parentPath);
        if (parentObj == null) return false;

        parentObj.Remove(key);

        return true;
    }

    public bool AddKey(string[] parentPath, string key, string kind)
    {
        if (value == null) value = new WscJsonObject();
        var parentObj = value.Qo().GetOrCreate(parentPath);

        if (kind == "object") parentObj[key] = new WscJsonObject();
        else parentObj[key] = "";

        return true;
    }

    public bool EditKey(string[] parentPath, string key, string value)
    {
        if (this.value == null) this.value = new WscJsonObject();

        var parentObj = this.value.Qo().GetOrCreate(parentPath);
        parentObj[key] = value;

        return true;
    }
}