using System.Reflection.Metadata;

namespace LocalizationEditor;

public enum FileType
{
    HJson,
    Json,
    Csv
}

public interface ILanguageFile
{
    string FilePath { get; set; }

    string ToJson();

    /// <summary>
    /// 从指定路径中加载
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否成功</returns>
    bool Load();

    /// <summary>
    /// 向指定路径中写入
    /// </summary>
    /// <param name="filePath">文件路径</param>
    void Save();

    /// <summary>
    /// 是否包含指定Key
    /// </summary>
    /// <param name="parentPath">前置路径</param>
    /// <param name="key">键</param>
    /// <returns>是否包含</returns>
    bool ContainsKey(string[] parentPath, string key);

    /// <summary>
    /// 重命名键
    /// </summary>
    /// <param name="parentPath">前置路径</param>
    /// <param name="oldKey">旧键</param>
    /// <param name="newKey">新键</param>
    /// <returns>是否脏</returns>
    bool RenameKey(string[] parentPath, string oldKey, string newKey);

    /// <summary>
    /// 删除键
    /// </summary>
    /// <param name="parentPath">前置路径</param>
    /// <param name="key">键</param>
    /// <returns>是否脏</returns>
    bool DeleteKey(string[] parentPath, string key);

    /// <summary>
    /// 创建键
    /// </summary>
    /// <param name="parentPath">前置路径</param>
    /// <param name="key">键</param>
    /// <param name="kind">类型</param>
    /// <returns>是否脏</returns>
    bool AddKey(string[] parentPath, string key, string kind);

    /// <summary>
    /// 编辑键
    /// </summary>
    /// <param name="parentPath">前置路径</param>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    /// <returns>是否脏</returns>
    bool EditKey(string[] parentPath, string key, string value);
}