namespace AisGpo.Api.Storage;

/// <summary>
/// Настройки файлового хранилища работ студентов.
/// </summary>
public sealed class FileStorageOptions
{
    /// <summary>
    /// Название секции конфигурации.
    /// </summary>
    public const string SectionName = "FileStorage";

    /// <summary>
    /// Корневая директория,
    /// в которой физически хранятся файлы.
    /// </summary>
    public string RootPath { get; init; } =
        ".local-storage/work-files";
}