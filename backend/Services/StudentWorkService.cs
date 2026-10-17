using AisGpo.Api.Common;
using AisGpo.Api.Contracts.Works;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using AisGpo.Api.Domain.Works;
using AisGpo.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Services;

/// <summary>
/// Сервис работы с отправками студентов.
///
/// Отвечает за проверку участия студента в проекте,
/// создание StudentWork, первой WorkRevision
/// и сохранение приложенных файлов.
/// </summary>
public sealed class StudentWorkService(
    AppDbContext db,
    IFileStorage fileStorage)
{
    /// <summary>
    /// Создаёт первую отправку работы студента.
    /// </summary>
    public async Task<StudentWorkResponse> SubmitAsync(
        long studentId,
        long projectId,
        SubmitWorkRequest request,
        CancellationToken ct)
    {
        var text = string.IsNullOrWhiteSpace(request.Text)
            ? null
            : request.Text.Trim();

        var files = request.Files
            .Where(x => x.Length > 0)
            .ToList();

        // Работа должна содержать хотя бы текст
        // или хотя бы один непустой файл.
        if (text is null && files.Count == 0)
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "WORK_CONTENT_REQUIRED",
                "Work must contain text or at least one file.");
        }

        // Отправлять работу может только студент,
        // который действительно является ACTIVE
        // участником указанного проекта.
        var membership = await db.ProjectMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.StudentId == studentId &&
                    x.ProjectId == projectId &&
                    x.Status == MembershipStatus.ACTIVE,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status403Forbidden,
                "WORK_SUBMISSION_FORBIDDEN",
                "Student is not an active participant of this project.");

        var project = await db.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == membership.ProjectId,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "PROJECT_NOT_FOUND",
                "Project not found.");

        var student = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == studentId,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "STUDENT_NOT_FOUND",
                "Student not found.");

        // Первая отправка создаёт StudentWork.
        // Повторная отправка будет реализована отдельно
        // как новая WorkRevision в BE-07.
        if (await db.StudentWorks.AnyAsync(
                x =>
                    x.StudentId == studentId &&
                    x.ProjectId == projectId,
                ct))
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "WORK_ALREADY_EXISTS",
                "Student work for this project already exists.");
        }

        var now = DateTimeOffset.UtcNow;

        var work = new StudentWork
        {
            StudentId = studentId,
            ProjectId = projectId,
            Status = WorkStatus.SUBMITTED,
            CreatedAt = now,
            UpdatedAt = now
        };

        var revision = new WorkRevision
        {
            StudentWork = work,
            VersionNumber = 1,
            Text = text,
            Status = WorkStatus.SUBMITTED,
            SubmittedAt = now
        };

        work.Revisions.Add(revision);

        // Файловое хранилище не является частью
        // транзакции PostgreSQL.
        //
        // Поэтому запоминаем все созданные storage keys:
        // если сохранение в БД завершится ошибкой,
        // физические файлы будут удалены.
        var storedKeys = new List<string>();

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

                storedKeys.Add(storageKey);

                revision.Files.Add(
                    new WorkFile
                    {
                        OriginalFileName =
                            GetSafeFileName(file.FileName),

                        StorageKey = storageKey,

                        ContentType =
                            string.IsNullOrWhiteSpace(
                                file.ContentType)
                                ? "application/octet-stream"
                                : file.ContentType,

                        SizeBytes = file.Length,
                        UploadedAt = now
                    });
            }

            db.StudentWorks.Add(work);

            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Если БД не сохранилась или загрузка
            // одного из файлов оборвалась,
            // удаляем уже сохранённые файлы.
            foreach (var storageKey in storedKeys)
            {
                try
                {
                    await fileStorage.DeleteIfExistsAsync(
                        storageKey,
                        CancellationToken.None);
                }
                catch
                {
                    // Ошибка очистки не должна скрывать
                    // исходную ошибку отправки работы.
                }
            }

            throw;
        }

        return new StudentWorkResponse(
            work.Id,
            project.Id,
            project.Name,
            student.Id,
            student.FullName,
            work.Status,
            work.CreatedAt,
            work.UpdatedAt,
            new WorkRevisionResponse(
                revision.Id,
                revision.VersionNumber,
                revision.Text,
                revision.Status,
                revision.Grade,
                revision.ReviewComment,
                revision.SubmittedAt,
                revision.ReviewedAt,
                revision.Files
                    .Select(file =>
                        new WorkFileResponse(
                            file.Id,
                            file.OriginalFileName,
                            file.ContentType,
                            file.SizeBytes))
                    .ToList()));
    }

    /// <summary>
    /// Убирает компоненты пути из пользовательского имени файла.
    /// </summary>
    private static string GetSafeFileName(
        string fileName)
    {
        var safeName =
            Path.GetFileName(fileName);

        if (string.IsNullOrWhiteSpace(safeName))
        {
            return "file";
        }

        // В БД OriginalFileName ограничен 255 символами.
        return safeName.Length <= 255
            ? safeName
            : safeName[..255];
    }
}