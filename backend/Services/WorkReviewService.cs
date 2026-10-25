using AisGpo.Api.Common;
using AisGpo.Api.Contracts.Works;
using AisGpo.Api.Data;
using AisGpo.Api.Domain.Works;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Services;

/// <summary>
/// Сервис проверки студенческих работ преподавателем.
/// </summary>
public sealed class WorkReviewService(
    AppDbContext db,
    WorkQueryService workQueryService)
{
    /// <summary>
    /// Принимает последнюю отправленную версию работы.
    /// </summary>
    public async Task<WorkDetailsResponse> AcceptAsync(
        long teacherId,
        long projectId,
        long workId,
        AcceptWorkRequest request,
        CancellationToken ct)
    {
        var work = await GetWorkForReviewAsync(
            teacherId,
            projectId,
            workId,
            ct);

        var revision = GetSubmittedRevision(work);

        revision.Status = WorkStatus.ACCEPTED;
        revision.Grade = request.Grade;
        revision.ReviewComment =
            NormalizeOptionalComment(request.Comment);
        revision.ReviewedById = teacherId;
        revision.ReviewedAt = DateTimeOffset.UtcNow;

        work.Status = WorkStatus.ACCEPTED;
        work.UpdatedAt = revision.ReviewedAt.Value;

        await db.SaveChangesAsync(ct);

        return await workQueryService.GetForTeacherAsync(
            teacherId,
            projectId,
            workId,
            ct);
    }

    /// <summary>
    /// Возвращает последнюю версию работы
    /// студенту на доработку.
    /// </summary>
    public async Task<WorkDetailsResponse> RequestRevisionAsync(
        long teacherId,
        long projectId,
        long workId,
        RequestWorkRevisionRequest request,
        CancellationToken ct)
    {
        var comment = request.Comment?.Trim();

        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new ApiException(
                StatusCodes.Status400BadRequest,
                "REVIEW_COMMENT_REQUIRED",
                "Review comment is required when requesting revision.");
        }

        var work = await GetWorkForReviewAsync(
            teacherId,
            projectId,
            workId,
            ct);

        var revision = GetSubmittedRevision(work);

        revision.Status = WorkStatus.NEEDS_REVISION;
        revision.Grade = null;
        revision.ReviewComment = comment;
        revision.ReviewedById = teacherId;
        revision.ReviewedAt = DateTimeOffset.UtcNow;

        work.Status = WorkStatus.NEEDS_REVISION;
        work.UpdatedAt = revision.ReviewedAt.Value;

        await db.SaveChangesAsync(ct);

        return await workQueryService.GetForTeacherAsync(
            teacherId,
            projectId,
            workId,
            ct);
    }

    /// <summary>
    /// Загружает работу и проверяет,
    /// что преподаватель руководит её проектом.
    /// </summary>
    private async Task<StudentWork> GetWorkForReviewAsync(
        long teacherId,
        long projectId,
        long workId,
        CancellationToken ct)
    {
        var work = await db.StudentWorks
            .Include(x => x.Project)
            .Include(x => x.Revisions)
            .SingleOrDefaultAsync(
                x => x.Id == workId,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "WORK_NOT_FOUND",
                "Student work not found.");

        // WorkId должен принадлежать проекту,
        // указанному в URL.
        if (work.ProjectId != projectId)
        {
            throw new ApiException(
                StatusCodes.Status404NotFound,
                "WORK_NOT_FOUND",
                "Student work not found.");
        }

        // Проверять работу имеет право только
        // преподаватель — руководитель проекта.
        if (work.Project.SupervisorId != teacherId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "WORK_REVIEW_ACCESS_DENIED",
                "Teacher can only review works from supervised projects.");
        }

        return work;
    }

    /// <summary>
    /// Возвращает последнюю версию и проверяет,
    /// что она ожидает проверки.
    /// </summary>
    private static WorkRevision GetSubmittedRevision(
        StudentWork work)
    {
        var revision = work.Revisions
            .OrderByDescending(x => x.VersionNumber)
            .FirstOrDefault()
            ?? throw new ApiException(
                StatusCodes.Status409Conflict,
                "WORK_HAS_NO_REVISIONS",
                "Student work has no revisions.");

        if (revision.Status != WorkStatus.SUBMITTED ||
            work.Status != WorkStatus.SUBMITTED)
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "INVALID_WORK_STATUS",
                "Only submitted work can be reviewed.");
        }

        return revision;
    }

    /// <summary>
    /// Нормализует необязательный комментарий.
    /// </summary>
    private static string? NormalizeOptionalComment(
        string? comment)
    {
        return string.IsNullOrWhiteSpace(comment)
            ? null
            : comment.Trim();
    }
}