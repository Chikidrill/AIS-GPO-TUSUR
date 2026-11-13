using AisGpo.Api.Common;
using AisGpo.Api.Contracts.Works;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using AisGpo.Api.Domain.Works;
using AisGpo.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Services;

/// <summary>
/// Сервис повторной отправки студенческой работы
/// после возврата преподавателем на доработку.
/// </summary>
public sealed class WorkResubmissionService(
    AppDbContext db,
    IFileStorage fileStorage,
    WorkQueryService workQueryService,
    WorkUploadValidator uploadValidator)
{
    /// <summary>
    /// Создаёт новую версию существующей работы.
    /// Предыдущие версии и результаты их проверки сохраняются.
    /// </summary>
    public async Task<WorkDetailsResponse> ResubmitAsync(
        long studentId,
        long workId,
        SubmitWorkRequest request,
        CancellationToken ct)
    {
        var text = string.IsNullOrWhiteSpace(request.Text)
            ? null
            : request.Text.Trim();

        var files = request.Files
            .Where(x => x.Length > 0)
            .ToList();

        if (text is null && files.Count == 0)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "WORK_CONTENT_REQUIRED",
                "Work must contain text, files, or both.");
        }

        await uploadValidator.ValidateAsync(
            files,
            ct);

        // Работа загружается без tracking,
        // поскольку её состояние далее меняется
        // атомарным ExecuteUpdateAsync.
        var work = await db.StudentWorks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == workId,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "WORK_NOT_FOUND",
                "Student work not found.");

        if (work.StudentId != studentId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "WORK_ACCESS_DENIED",
                "Student can only access their own work.");
        }

        if (work.Status != WorkStatus.NEEDS_REVISION)
        {
            throw CreateResubmissionConflict();
        }

        var hasActiveMembership =
            await db.ProjectMemberships
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.ProjectId == work.ProjectId &&
                        x.StudentId == studentId &&
                        x.Status == MembershipStatus.ACTIVE,
                    ct);

        if (!hasActiveMembership)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "WORK_SUBMISSION_FORBIDDEN",
                "Only an active project participant can submit work.");
        }

        var now = DateTimeOffset.UtcNow;

        // Файлы сохраняются до DB-транзакции,
        // чтобы не держать блокировку строки работы
        // во время потенциально долгой записи файлов.
        //
        // Если конкурентный запрос победит,
        // эти файлы будут удалены в catch.
        var storedFiles =
            new List<StoredWorkFile>();

        try
        {
            foreach (var file in files)
            {
                await using var stream =
                    file.OpenReadStream();

                var storageKey =
                    await fileStorage.SaveAsync(
                        stream,
                        ct);

                var fileName =
                    GetSafeFileName(
                        file.FileName);

                storedFiles.Add(
                    new StoredWorkFile(
                        storageKey,
                        fileName,
                        string.IsNullOrWhiteSpace(
                            file.ContentType)
                            ? "application/octet-stream"
                            : file.ContentType,
                        file.Length));
            }

            await using var transaction =
                await db.Database
                    .BeginTransactionAsync(ct);

            // Только один параллельный запрос
            // может перевести работу из
            // NEEDS_REVISION обратно в SUBMITTED.
            //
            // Второй запрос после ожидания блокировки
            // увидит уже изменённый status
            // и обновит 0 строк.
            var workUpdated =
                await db.StudentWorks
                    .Where(x =>
                        x.Id == workId &&
                        x.StudentId == studentId &&
                        x.Status ==
                            WorkStatus.NEEDS_REVISION)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(
                                x => x.Status,
                                WorkStatus.SUBMITTED)
                            .SetProperty(
                                x => x.UpdatedAt,
                                now),
                        ct);

            if (workUpdated != 1)
            {
                await transaction
                    .RollbackAsync(ct);

                throw CreateResubmissionConflict();
            }

            // После успешного условного UPDATE
            // этот запрос является единственным,
            // кто имеет право создать следующую версию.
            var lastVersion =
                await db.WorkRevisions
                    .Where(x =>
                        x.StudentWorkId == workId)
                    .Select(x =>
                        (int?)x.VersionNumber)
                    .MaxAsync(ct)
                ?? 0;

            var revision =
                new WorkRevision
                {
                    StudentWorkId = workId,
                    VersionNumber =
                        lastVersion + 1,
                    Text = text,
                    Status =
                        WorkStatus.SUBMITTED,
                    SubmittedAt = now
                };

            foreach (var storedFile in
                     storedFiles)
            {
                revision.Files.Add(
                    new WorkFile
                    {
                        OriginalFileName =
                            storedFile.FileName,

                        StorageKey =
                            storedFile.StorageKey,

                        ContentType =
                            storedFile.ContentType,

                        SizeBytes =
                            storedFile.SizeBytes,

                        UploadedAt = now
                    });
            }

            db.WorkRevisions.Add(
                revision);

            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            // Ошибка очистки физического файла
            // не должна скрывать исходную ошибку
            // базы или бизнес-логики.
            foreach (var storedFile in
                     storedFiles)
            {
                try
                {
                    await fileStorage
                        .DeleteIfExistsAsync(
                            storedFile.StorageKey,
                            CancellationToken.None);
                }
                catch
                {
                    // Ошибка cleanup не заменяет
                    // исходное исключение.
                }
            }

            throw;
        }

        return await workQueryService
            .GetForStudentAsync(
                studentId,
                workId,
                ct);
    }

    /// <summary>
    /// Возвращает безопасное пользовательское
    /// имя файла для хранения в базе.
    /// </summary>
    private static string GetSafeFileName(
        string fileName)
    {
        // Нормализуем оба варианта разделителя,
        // поскольку имя приходит от клиента
        // и не обязано соответствовать ОС сервера.
        var normalized =
            fileName.Replace(
                '\\',
                '/');

        var name =
            normalized
                .Split(
                    '/',
                    StringSplitOptions
                        .RemoveEmptyEntries)
                .LastOrDefault()
            ?? "file";

        if (string.IsNullOrWhiteSpace(name))
        {
            name = "file";
        }

        if (name.Length > 255)
        {
            name = name[..255];
        }

        return name;
    }

    /// <summary>
    /// Создаёт стандартный конфликт,
    /// если повторная отправка уже выполнена
    /// другим запросом либо больше не разрешена.
    /// </summary>
    private static ApiException
        CreateResubmissionConflict()
    {
        return new ApiException(
            StatusCodes.Status409Conflict,
            "WORK_RESUBMISSION_NOT_ALLOWED",
            "Work can only be resubmitted after revision is requested.");
    }

    /// <summary>
    /// Временные метаданные уже сохранённого
    /// физического файла до записи версии в БД.
    /// </summary>
    private sealed record StoredWorkFile(
        string StorageKey,
        string FileName,
        string ContentType,
        long SizeBytes);
}