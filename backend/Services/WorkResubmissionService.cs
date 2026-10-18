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
    WorkQueryService workQueryService)
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

        var work = await db.StudentWorks
            .Include(x => x.Revisions)
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
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "WORK_RESUBMISSION_NOT_ALLOWED",
                "Work can only be resubmitted after revision is requested.");
        }

        var hasActiveMembership =
            await db.ProjectMemberships.AnyAsync(
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

        var nextVersion = work.Revisions.Count == 0
            ? 1
            : work.Revisions.Max(x => x.VersionNumber) + 1;

        var revision = new WorkRevision
        {
            StudentWorkId = work.Id,
            VersionNumber = nextVersion,
            Text = text,
            Status = WorkStatus.SUBMITTED,
            SubmittedAt = now
        };

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

                var fileName =
                    Path.GetFileName(file.FileName);

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName = "file";
                }

                if (fileName.Length > 255)
                {
                    fileName = fileName[..255];
                }

                revision.Files.Add(
                    new WorkFile
                    {
                        OriginalFileName = fileName,
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

            work.Revisions.Add(revision);
            work.Status = WorkStatus.SUBMITTED;
            work.UpdatedAt = now;

            await db.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var storageKey in storedKeys)
            {
                await fileStorage.DeleteIfExistsAsync(
                    storageKey,
                    CancellationToken.None);
            }

            throw;
        }

        return await workQueryService.GetForStudentAsync(
            studentId,
            workId,
            ct);
    }
}