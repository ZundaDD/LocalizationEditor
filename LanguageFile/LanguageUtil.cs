namespace LocalizationEditor;

public static partial class LanguageUtil
{
    public static ILanguageFile Load(string filePath)
    {
        if (Path.GetExtension(filePath) == ".hjson")
        {
            var hjson = new HjsonFile();
            hjson.FilePath = filePath;
            var success = hjson.Load();
            return success ? hjson : null!;
        }

        return null!;
    }
}