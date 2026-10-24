using Microsoft.Extensions.Options;

namespace AisGpo.Api.Storage;

/// <summary>
/// Реализация файлового хранилища
/// на локальной файловой системе.
///
/// В Docker корневая директория
/// будет подключена как persistent volume.
/// </summary>
public sealed class LocalFileStorage : IFileStorage
{
    private readonly string rootPath;

    /// <summary>
    /// Создаёт локальное файловое хранилище.
    /// </summary>
    public LocalFileStorage(
        IOptions<FileStorageOptions> options,
        IHostEnvironment environment)
    {
        var configuredPath =
            options.Value.RootPath;

        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException(
                "FileStorage:RootPath is required.");
        }

        // Для локального запуска относительный путь
        // считается от директории backend.
        rootPath = Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(
                Path.Combine(
                    environment.ContentRootPath,
                    configuredPath));

        Directory.CreateDirectory(rootPath);
    }

    /// <inheritdoc />
    public async Task<string> SaveAsync(
        Stream content,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        // Пользовательское имя файла здесь
        // намеренно не используется.
        //
        // Физическое имя генерируется сервером,
        // поэтому через имя загружаемого файла
        // нельзя выполнить path traversal.
        var storageKey =
            Guid.NewGuid().ToString("N");

        var path =
            ResolvePath(storageKey);

        try
        {
            await using var output =
                new FileStream(
                    path,
                    new FileStreamOptions
                    {
                        Mode = FileMode.CreateNew,
                        Access = FileAccess.Write,
                        Share = FileShare.None,
                        Options =
                            FileOptions.Asynchronous |
                            FileOptions.SequentialScan
                    });

            await content.CopyToAsync(
                output,
                ct);

            await output.FlushAsync(ct);
        }
        catch
        {
            // Если загрузка оборвалась,
            // не оставляем частично записанный файл.
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            throw;
        }

        return storageKey;
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(
        string storageKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path =
            ResolvePath(storageKey);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Stored file was not found.",
                storageKey);
        }

        Stream stream =
            new FileStream(
                path,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read,
                    Options =
                        FileOptions.Asynchronous |
                        FileOptions.SequentialScan
                });

        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public Task DeleteIfExistsAsync(
        string storageKey,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path =
            ResolvePath(storageKey);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Преобразует внутренний ключ
    /// в безопасный физический путь.
    /// </summary>
    private string ResolvePath(
        string storageKey)
    {
        // Наши storage key всегда являются GUID
        // без дефисов и без частей пути.
        if (!Guid.TryParseExact(
                storageKey,
                "N",
                out _))
        {
            throw new InvalidOperationException(
                "Invalid storage key.");
        }

        var path =
            Path.GetFullPath(
                Path.Combine(
                    rootPath,
                    storageKey));

        var relativePath =
            Path.GetRelativePath(
                rootPath,
                path);

        if (relativePath == ".." ||
            relativePath.StartsWith(
                $"..{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal) ||
            Path.IsPathRooted(relativePath))
        {
            throw new InvalidOperationException(
                "Storage path is outside the configured root.");
        }

        return path;
    }
}