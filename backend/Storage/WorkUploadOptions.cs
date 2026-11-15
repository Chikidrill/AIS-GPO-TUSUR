namespace AisGpo.Api.Storage;

/// <summary>
/// Ограничения загрузки файлов студенческих работ.
/// </summary>
public sealed class WorkUploadOptions
{
    /// <summary>
    /// Название секции конфигурации.
    /// </summary>
    public const string SectionName = "WorkUpload";

    /// <summary>
    /// Максимальное количество файлов
    /// в одной версии работы.
    /// </summary>
    public int MaxFiles { get; init; } = 5;

    /// <summary>
    /// Максимальный размер одного файла в байтах.
    /// По умолчанию 20 MiB.
    /// </summary>
    public long MaxFileSizeBytes { get; init; } =
        20L * 1024 * 1024;

    /// <summary>
    /// Максимальный суммарный размер файлов
    /// одной версии работы.
    /// По умолчанию 50 MiB.
    /// </summary>
    public long MaxTotalSizeBytes { get; init; } =
        50L * 1024 * 1024;

    /// <summary>
    /// Максимальный размер всего HTTP-запроса
    /// с multipart/form-data.
    ///
    /// Значение немного больше бизнес-лимита файлов,
    /// чтобы оставить место для multipart-обвязки и текста.
    /// </summary>
    public long MaxRequestBodySizeBytes { get; init; } =
        60L * 1024 * 1024;

    /// <summary>
    /// Разрешённые расширения файлов.
    /// </summary>
    public string[] AllowedExtensions { get; init; } =
    [
        ".pdf",
        ".doc",
        ".docx",
        ".txt",
        ".png",
        ".jpg",
        ".jpeg",
        ".zip"
    ];
}