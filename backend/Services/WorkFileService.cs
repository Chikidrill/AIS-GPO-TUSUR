using AisGpo.Api.Common;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using AisGpo.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Services;

/// <summary>
/// Результат открытия файла работы.
/// </summary>
public sealed record WorkFileDownload(
    Stream Content,
    string FileName,
    string ContentType);

/// <summary>
/// Сервис защищённого доступа
/// к файлам студенческих работ.
/// </summary>
public sealed class WorkFileService(
    AppDbContext db,
    IFileStorage fileStorage)
{
    /// <summary>
    /// Открывает файл после проверки,
    /// имеет ли текущий пользователь право его читать.
    /// </summary>
    public async Task<WorkFileDownload> OpenAsync(
        long userId,
        UserRole role,
        long fileId,
        CancellationToken ct)
    {
        var file = await db.WorkFiles
            .AsNoTracking()
            .Include(x => x.WorkRevision)
                .ThenInclude(x => x.StudentWork)
                    .ThenInclude(x => x.Project)
            .SingleOrDefaultAsync(
                x => x.Id == fileId,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "WORK_FILE_NOT_FOUND",
                "Work file not found.");

        var work =
            file.WorkRevision.StudentWork;

        var hasAccess = role switch
        {
            UserRole.STUDENT =>
                work.StudentId == userId,

            UserRole.TEACHER =>
                work.Project.SupervisorId == userId,

            // Администратору разрешаем
            // read-only административный доступ.
            UserRole.ADMIN => true,

            _ => false
        };

        if (!hasAccess)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "WORK_FILE_ACCESS_DENIED",
                "User does not have access to this work file.");
        }

        Stream content;

        try
        {
            content = await fileStorage.OpenReadAsync(
                file.StorageKey,
                ct);
        }
        catch (FileNotFoundException)
        {
            // Запись существует в БД,
            // но физический файл отсутствует.
            throw new ApiException(
                StatusCodes.Status404NotFound,
                "WORK_FILE_CONTENT_NOT_FOUND",
                "Stored work file was not found.");
        }

        return new WorkFileDownload(
            content,
            file.OriginalFileName,
            file.ContentType);
    }
}