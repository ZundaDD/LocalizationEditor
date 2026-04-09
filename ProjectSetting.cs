namespace LocalizationEditor;

public class ProjectConfig
{
    public string ProjectName { get; set; } = "Unnamed project";

    public string FileType { get; set; } = "hjson";

    public string SourceLanguage { get; set; } = "";

    public Dictionary<string, string> Files { get; set; } = new();

    public bool IsL10nProject = false;
}