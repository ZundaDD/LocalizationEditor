using System.Text.Json;
using System.Text.RegularExpressions;
using Hjson;

namespace LocalizationEditor;

public class HjsonConverter
{
    public JsonValue SaveTo(string json, string path)
    {
        var parseOptions = new HjsonOptions { KeepWsc = true };
        var hjsonVal = HjsonValue.Parse(json, parseOptions);

        var writeOptions = new HjsonOptions { EmitRootBraces = false, };

        var str = hjsonVal.ToString(writeOptions);

        File.WriteAllText(path, str);
        return hjsonVal;
    }

    public JsonValue LoadFrom(string path)
    {
       return HjsonValue.Load(path);
    }
}