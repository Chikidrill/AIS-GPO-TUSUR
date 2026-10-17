using AisGpo.Api.Common;
using AisGpo.Api.Contracts.Works;
using AisGpo.Api.Data;
using AisGpo.Api.Domain.Works;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Services;

/// <summary>
/// Сервис чтения работ студентов.
///
/// Отвечает за получение списков работ,
/// подробной информации и проверку доступа
/// студента и преподавателя.
/// </summary>
public sealed class WorkQueryService(
    AppDbContext db)
{
    /// <summary>
    /// Возвращает работы текущего студента.
    /// </summary>
    public async Task<IReadOnlyList<WorkSummaryResponse>>
        ListForStudentAsync(
            long studentId,
            CancellationToken ct)
    {
        var works = await db.StudentWorks
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.Student)
            .Include(x => x.Revisions)
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);

        return await MapSummariesAsync(
            works,
            ct);
    }

    /// <summary>
    /// Возвращает работы студентов конкретного проекта
    /// для преподавателя-руководителя.
    /// </summary>
    public async Task<IReadOnlyList<WorkSummaryResponse>>
        ListForTeacherAsync(
            long teacherId,
            long projectId,
            CancellationToken ct)
    {
        var project = await db.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "PROJECT_NOT_FOUND",
                "Project not found.");

        // Одной роли TEACHER недостаточно.
        // Преподаватель должен быть руководителем
        // именно этого проекта.
        if (project.SupervisorId != teacherId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "PROJECT_ACCESS_DENIED",
                "Teacher is not the supervisor of this project.");
        }

        var works = await db.StudentWorks
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.Student)
            .Include(x => x.Revisions)
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);

        return await MapSummariesAsync(
            works,
            ct);
    }

    /// <summary>
    /// Возвращает подробную информацию
    /// о собственной работе студента.
    /// </summary>
    public async Task<WorkDetailsResponse>
        GetForStudentAsync(
            long studentId,
            long workId,
            CancellationToken ct)
    {
        var work = await GetDetailsEntityAsync(
            workId,
            ct);

        if (work.StudentId != studentId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "WORK_ACCESS_DENIED",
                "Student can only access their own work.");
        }

        return await MapDetailsAsync(
            work,
            ct);
    }

    /// <summary>
    /// Возвращает подробную информацию о работе
    /// преподавателю-руководителю проекта.
    /// </summary>
    public async Task<WorkDetailsResponse>
     GetForTeacherAsync(
         long teacherId,
         long projectId,
         long workId,
         CancellationToken ct)
    {
        var work = await GetDetailsEntityAsync(
            workId,
            ct);

        // WorkId должен принадлежать именно проекту,
        // указанному в URL.
        if (work.ProjectId != projectId)
        {
            throw new ApiException(
                StatusCodes.Status404NotFound,
                "WORK_NOT_FOUND",
                "Student work not found.");
        }

        // Одной роли TEACHER недостаточно:
        // преподаватель должен руководить
        // именно этим проектом.
        if (work.Project.SupervisorId != teacherId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "WORK_ACCESS_DENIED",
                "Teacher can only access works from supervised projects.");
        }

        return await MapDetailsAsync(
            work,
            ct);
    }

    /// <summary>
    /// Загружает работу со всеми версиями,
    /// файлами, студентом и проектом.
    /// </summary>
    private async Task<StudentWork>
        GetDetailsEntityAsync(
            long workId,
            CancellationToken ct)
    {
        return await db.StudentWorks
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.Student)
            .Include(x => x.Revisions)
                .ThenInclude(x => x.Files)
            .SingleOrDefaultAsync(
                x => x.Id == workId,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "WORK_NOT_FOUND",
                "Student work not found.");
    }

    /// <summary>
    /// Преобразует набор работ
    /// в краткие DTO для списков.
    /// </summary>
    private async Task<IReadOnlyList<WorkSummaryResponse>>
        MapSummariesAsync(
            IReadOnlyList<StudentWork> works,
            CancellationToken ct)
    {
        if (works.Count == 0)
        {
            return Array.Empty<WorkSummaryResponse>();
        }

        var studentIds = works
            .Select(x => x.StudentId)
            .Distinct()
            .ToList();

        var groups = await db.StudentProfiles
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.UserId))
            .ToDictionaryAsync(
                x => x.UserId,
                x => x.GroupNumber,
                ct);

        return works
            .Select(work =>
            {
                var latestRevision = work.Revisions
                    .OrderByDescending(x => x.VersionNumber)
                    .First();

                groups.TryGetValue(
                    work.StudentId,
                    out var groupNumber);

                return new WorkSummaryResponse(
                    work.Id,
                    work.ProjectId,
                    work.Project.Name,
                    work.StudentId,
                    work.Student.FullName,
                    groupNumber,
                    work.Status,
                    latestRevision.VersionNumber,
                    latestRevision.SubmittedAt,
                    work.UpdatedAt);
            })
            .ToList();
    }

    /// <summary>
    /// Преобразует работу
    /// в подробный DTO со всей историей версий.
    /// </summary>
    private async Task<WorkDetailsResponse>
        MapDetailsAsync(
            StudentWork work,
            CancellationToken ct)
    {
        var groupNumber = await db.StudentProfiles
            .AsNoTracking()
            .Where(x => x.UserId == work.StudentId)
            .Select(x => x.GroupNumber)
            .SingleOrDefaultAsync(ct);

        var revisions = work.Revisions
            .OrderBy(x => x.VersionNumber)
            .Select(revision =>
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
                        .OrderBy(x => x.Id)
                        .Select(file =>
                            new WorkFileResponse(
                                file.Id,
                                file.OriginalFileName,
                                file.ContentType,
                                file.SizeBytes))
                        .ToList()))
            .ToList();

        return new WorkDetailsResponse(
            work.Id,
            work.ProjectId,
            work.Project.Name,
            work.StudentId,
            work.Student.FullName,
            groupNumber,
            work.Status,
            work.CreatedAt,
            work.UpdatedAt,
            revisions);
    }
}