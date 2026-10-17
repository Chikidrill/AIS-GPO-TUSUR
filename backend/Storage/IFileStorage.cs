namespace AisGpo.Api.Storage;

/// <summary>
/// Абстракция файлового хранилища.
///
/// Бизнес-логика не должна знать,
/// где именно физически находится файл:
/// на локальном диске, Docker volume,
/// S3-совместимом хранилище и т. д.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Сохраняет содержимое файла
    /// и возвращает внутренний ключ хранилища.
    /// </summary>
    Task<string> SaveAsync(
        Stream content,
        CancellationToken ct = default);

    /// <summary>
    /// Открывает сохранённый файл для чтения.
    /// </summary>
    Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken ct = default);

    /// <summary>
    /// Удаляет файл, если он существует.
    /// </summary>
    Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken ct = default);
}