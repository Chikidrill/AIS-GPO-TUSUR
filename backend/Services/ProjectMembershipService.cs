using AisGpo.Api.Common;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Services;

/// <summary>
/// Сервис управления участниками проектов.
/// </summary>
public sealed class ProjectMembershipService(
    AppDbContext db)
{
    /// <summary>
    /// Исключает активного участника из проекта,
    /// сохраняя историю участия.
    /// </summary>
    public async Task ExcludeAsync(
        long actorId,
        UserRole actorRole,
        long projectId,
        long studentId,
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

        // Администратор может управлять участниками
        // любого проекта.
        //
        // Преподаватель может исключать участников
        // только из проекта, которым он руководит.
        if (actorRole == UserRole.TEACHER &&
            project.SupervisorId != actorId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "PROJECT_ACCESS_DENIED",
                "Teacher is not the supervisor of this project.");
        }

        if (actorRole != UserRole.ADMIN &&
            actorRole != UserRole.TEACHER)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "MEMBERSHIP_MANAGEMENT_FORBIDDEN",
                "Current user cannot manage project participants.");
        }

        var membership = await db.ProjectMemberships
            .SingleOrDefaultAsync(
                x =>
                    x.ProjectId == projectId &&
                    x.StudentId == studentId &&
                    x.Status == MembershipStatus.ACTIVE,
                ct);

        if (membership is null)
        {
            var hasHistory =
                await db.ProjectMemberships.AnyAsync(
                    x =>
                        x.ProjectId == projectId &&
                        x.StudentId == studentId,
                    ct);

            if (hasHistory)
            {
                throw new ApiException(
                    StatusCodes.Status409Conflict,
                    "MEMBERSHIP_NOT_ACTIVE",
                    "Student is not an active participant of this project.");
            }

            throw new ApiException(
                StatusCodes.Status404NotFound,
                "MEMBERSHIP_NOT_FOUND",
                "Project membership not found.");
        }

        membership.Status =
            MembershipStatus.EXCLUDED;

        membership.LeftAt =
            DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
    }
}