using System.IO.Compression;
using AisGpo.Api.Common;
using AisGpo.Api.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AisGpo.Api.Services;

/// <summary>
/// ѕровер€ет файлы, прикладываемые
/// к студенческим работам.
/// </summary>
public sealed class WorkUploadValidator
{
    private readonly WorkUploadOptions options;
    private readonly HashSet<string> allowedExtensions;

    /// <summary>
    /// —оздаЄт валидатор загрузок.
    /// </summary>
    public WorkUploadValidator(
        IOptions<WorkUploadOptions> options)
    {
        this.options = options.Value;

        if (this.options.MaxFiles <= 0)
        {
            throw new InvalidOperationException(
                "WorkUpload:MaxFiles must be greater than zero.");
        }

        if (this.options.MaxFileSizeBytes <= 0)
        {
            throw new InvalidOperationException(
                "WorkUpload:MaxFileSizeBytes must be greater than zero.");
        }

        if (this.options.MaxTotalSizeBytes <= 0)
        {
            throw new InvalidOperationException(
                "WorkUpload:MaxTotalSizeBytes must be greater than zero.");
        }

        if (this.options.MaxRequestBodySizeBytes <= 0)
        {
            throw new InvalidOperationException(
                "WorkUpload:MaxRequestBodySizeBytes must be greater than zero.");
        }

        if (this.options.MaxTotalSizeBytes >
            this.options.MaxRequestBodySizeBytes)
        {
            throw new InvalidOperationException(
                "WorkUpload:MaxTotalSizeBytes cannot exceed MaxRequestBodySizeBytes.");
        }

        allowedExtensions = this.options.AllowedExtensions
            .Select(NormalizeExtension)
            .Where(x => x.Length > 1)
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        if (allowedExtensions.Count == 0)
        {
            throw new InvalidOperationException(
                "WorkUpload:AllowedExtensions must contain at least one extension.");
        }
    }

    /// <summary>
    /// ѕровер€ет количество, размеры,
    /// расширени€ и содержимое файлов.
    /// </summary>
    public async Task ValidateAsync(
        IReadOnlyCollection<IFormFile> files,
        CancellationToken ct)
    {
        var nonEmptyFiles = files
            .Where(x => x.Length > 0)
            .ToList();

        if (nonEmptyFiles.Count >
            options.MaxFiles)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "TOO_MANY_WORK_FILES",
                $"A work revision can contain at most {options.MaxFiles} files.");
        }

        long totalSize = 0;

        foreach (var file in nonEmptyFiles)
        {
            ct.ThrowIfCancellationRequested();

            if (file.Length >
                options.MaxFileSizeBytes)
            {
                throw new ApiException(
                    StatusCodes.Status413PayloadTooLarge,
                    "WORK_FILE_TOO_LARGE",
                    $"File '{GetSafeDisplayName(file.FileName)}' exceeds the maximum allowed size.");
            }

            try
            {
                totalSize = checked(
                    totalSize +
                    file.Length);
            }
            catch (OverflowException)
            {
                throw new ApiException(
                    StatusCodes.Status413PayloadTooLarge,
                    "WORK_FILES_TOO_LARGE",
                    "The total size of uploaded files is too large.");
            }

            if (totalSize >
                options.MaxTotalSizeBytes)
            {
                throw new ApiException(
                    StatusCodes.Status413PayloadTooLarge,
                    "WORK_FILES_TOO_LARGE",
                    "The total size of uploaded files exceeds the maximum allowed size.");
            }

            var extension =
                NormalizeExtension(
                    Path.GetExtension(
                        file.FileName));

            if (string.IsNullOrWhiteSpace(
                    extension) ||
                !allowedExtensions.Contains(
                    extension))
            {
                throw new ApiException(
                    StatusCodes.Status400BadRequest,
                    "WORK_FILE_TYPE_NOT_ALLOWED",
                    string.IsNullOrWhiteSpace(
                        extension)
                        ? "Files without an allowed extension are not permitted."
                        : $"Files with extension '{extension}' are not allowed.");
            }

            await ValidateSignatureAsync(
                file,
                extension,
                ct);
        }
    }

    /// <summary>
    /// ѕровер€ет бинарные форматы
    /// по фактическому содержимому файла.
    ///
    /// Content-Type от клиента намеренно
    /// не считаетс€ доверенным источником.
    /// </summary>
    private static async Task ValidateSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken ct)
    {
        // TXT не имеет единой надЄжной
        // бинарной сигнатуры.
        if (extension == ".txt")
        {
            return;
        }

        var header =
            new byte[8];

        int bytesRead;

        // ѕоток закрываетс€ до дополнительной
        // проверки структуры DOCX.
        await using (var stream =
            file.OpenReadStream())
        {
            bytesRead =
                await stream.ReadAsync(
                    header.AsMemory(
                        0,
                        header.Length),
                    ct);
        }

        var valid =
            extension switch
            {
                ".pdf" =>
                    StartsWith(
                        header,
                        bytesRead,
                        [
                            0x25,
                            0x50,
                            0x44,
                            0x46,
                            0x2D
                        ]),

                ".png" =>
                    StartsWith(
                        header,
                        bytesRead,
                        [
                            0x89,
                            0x50,
                            0x4E,
                            0x47,
                            0x0D,
                            0x0A,
                            0x1A,
                            0x0A
                        ]),

                ".jpg" or ".jpeg" =>
                    StartsWith(
                        header,
                        bytesRead,
                        [
                            0xFF,
                            0xD8,
                            0xFF
                        ]),

                ".doc" =>
                    StartsWith(
                        header,
                        bytesRead,
                        [
                            0xD0,
                            0xCF,
                            0x11,
                            0xE0,
                            0xA1,
                            0xB1,
                            0x1A,
                            0xE1
                        ]),

                // DOCX €вл€етс€ ZIP-контейнером.
                // ѕровер€ем как ZIP-сигнатуру,
                // так и об€зательные части документа.
                ".docx" =>
                    HasZipSignature(
                        header,
                        bytesRead) &&
                    IsDocx(file),

                ".zip" =>
                    HasZipSignature(
                        header,
                        bytesRead),

                _ => false
            };

        if (!valid)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "WORK_FILE_CONTENT_INVALID",
                $"File '{GetSafeDisplayName(file.FileName)}' does not match its declared file type.");
        }
    }

    /// <summary>
    /// ѕровер€ет начало буфера
    /// на ожидаемую бинарную сигнатуру.
    /// </summary>
    private static bool StartsWith(
        byte[] buffer,
        int bytesRead,
        byte[] signature)
    {
        if (bytesRead <
            signature.Length)
        {
            return false;
        }

        for (var i = 0;
             i < signature.Length;
             i++)
        {
            if (buffer[i] !=
                signature[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// ѕровер€ет известные варианты
    /// заголовка ZIP-контейнера.
    /// </summary>
    private static bool HasZipSignature(
        byte[] buffer,
        int bytesRead)
    {
        return
            StartsWith(
                buffer,
                bytesRead,
                [
                    0x50,
                    0x4B,
                    0x03,
                    0x04
                ]) ||
            StartsWith(
                buffer,
                bytesRead,
                [
                    0x50,
                    0x4B,
                    0x05,
                    0x06
                ]) ||
            StartsWith(
                buffer,
                bytesRead,
                [
                    0x50,
                    0x4B,
                    0x07,
                    0x08
                ]);
    }

    /// <summary>
    /// ѕровер€ет, что ZIP-контейнер
    /// действительно имеет структуру DOCX.
    /// </summary>
    private static bool IsDocx(
        IFormFile file)
    {
        try
        {
            using var stream =
                file.OpenReadStream();

            using var archive =
                new ZipArchive(
                    stream,
                    ZipArchiveMode.Read,
                    leaveOpen: false);

            var hasContentTypes =
                archive.GetEntry(
                    "[Content_Types].xml")
                is not null;

            var hasWordDocument =
                archive.GetEntry(
                    "word/document.xml")
                is not null;

            return
                hasContentTypes &&
                hasWordDocument;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>
    /// Ќормализует расширение
    /// к формату ".pdf".
    /// </summary>
    private static string NormalizeExtension(
        string? extension)
    {
        if (string.IsNullOrWhiteSpace(
                extension))
        {
            return string.Empty;
        }

        var normalized =
            extension
                .Trim()
                .ToLowerInvariant();

        return normalized.StartsWith('.')
            ? normalized
            : $".{normalized}";
    }

    /// <summary>
    /// ¬озвращает безопасное им€ файла
    /// дл€ использовани€ в сообщени€х.
    /// </summary>
    private static string GetSafeDisplayName(
        string fileName)
    {
        var name =
            Path.GetFileName(
                fileName);

        return string.IsNullOrWhiteSpace(
            name)
            ? "file"
            : name;
    }
}