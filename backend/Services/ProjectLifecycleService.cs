using AisGpo.Api.Common;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using AisGpo.Api.Domain.Works;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Services;

/// <summary>
/// ”правл€ет жизненным циклом проекта
/// и св€занными состо€ни€ми за€вок и участников.
/// </summary>
public sealed class ProjectLifecycleService(
    AppDbContext db)
{
    /// <summary>
    /// »змен€ет статус проекта с проверкой
    /// св€занных бизнес-ограничений.
    /// </summary>
    public async Task<Project> ChangeStatusAsync(
        long projectId,
        ProjectStatus nextStatus,
        CancellationToken ct)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(ct);

        // —трока проекта блокируетс€ до конца транзакции.
        //
        // Ёто не позвол€ет одновременно сн€ть
        // руководител€ и перевести проект в IN_PROGRESS.
        var project =
            await GetLockedProjectAsync(
                projectId,
                ct);

        // ѕовторна€ установка текущего статуса
        // €вл€етс€ идемпотентной операцией.
        if (project.Status == nextStatus)
        {
            await transaction.CommitAsync(ct);
            return project;
        }

        if (!IsAllowedStatusTransition(
                project.Status,
                nextStatus))
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "INVALID_PROJECT_STATUS_TRANSITION",
                $"Project status cannot be changed from {project.Status} to {nextStatus}.");
        }

        // Ќельз€ запускать проект,
        // если руководитель ещЄ не назначен.
        if (project.Status == ProjectStatus.OPEN &&
            nextStatus == ProjectStatus.IN_PROGRESS &&
            project.SupervisorId is null)
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "PROJECT_SUPERVISOR_REQUIRED",
                "Project must have a supervisor before it can be started.");
        }

        // Ќельз€ возвращать проект с участниками
        // обратно в состо€ние черновика.
        if (project.Status == ProjectStatus.OPEN &&
            nextStatus == ProjectStatus.DRAFT)
        {
            var hasActiveParticipants =
                await db.ProjectMemberships
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.ProjectId == projectId &&
                            x.Status ==
                                MembershipStatus.ACTIVE,
                        ct);

            if (hasActiveParticipants)
            {
                throw new ApiException(
                    StatusCodes.Status409Conflict,
                    "PROJECT_HAS_ACTIVE_PARTICIPANTS",
                    "Project with active participants cannot be returned to draft.");
            }
        }

        // Ќормальное завершение проекта возможно
        // только после обработки всех работ.
        if (project.Status == ProjectStatus.IN_PROGRESS &&
            nextStatus == ProjectStatus.COMPLETED)
        {
            var hasUnfinishedWorks =
                await db.StudentWorks
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.ProjectId == projectId &&
                            (
                                x.Status ==
                                    WorkStatus.SUBMITTED ||
                                x.Status ==
                                    WorkStatus.NEEDS_REVISION
                            ),
                        ct);

            if (hasUnfinishedWorks)
            {
                throw new ApiException(
                    StatusCodes.Status409Conflict,
                    "PROJECT_HAS_UNFINISHED_WORKS",
                    "Project cannot be completed while it has unfinished student works.");
            }
        }

        var now =
            DateTimeOffset.UtcNow;

        //  ак только проект перестаЄт быть OPEN,
        // незавершЄнные за€вки на вступление
        // больше не могут быть одобрены.
        if (project.Status == ProjectStatus.OPEN &&
            nextStatus != ProjectStatus.OPEN)
        {
            await db.ParticipationApplications
                .Where(x =>
                    x.ProjectId == projectId &&
                    (
                        x.Status ==
                            ApplicationStatus.CREATED ||
                        x.Status ==
                            ApplicationStatus.UNDER_REVIEW
                    ))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.Status,
                            ApplicationStatus.CANCELLED),
                    ct);
        }

        if (nextStatus is
            ProjectStatus.COMPLETED or
            ProjectStatus.ARCHIVED)
        {
            // ѕосле закрыти€ проекта pending-за€вки
            // на добровольный выход тер€ют смысл.
            await db.ProjectLeaveRequests
                .Where(x =>
                    x.ProjectId == projectId &&
                    (
                        x.Status ==
                            LeaveRequestStatus.CREATED ||
                        x.Status ==
                            LeaveRequestStatus.UNDER_REVIEW
                    ))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.Status,
                            LeaveRequestStatus.CANCELLED),
                    ct);

            // ACTIVE означает текущее участие.
            //
            // ѕри завершении либо архивировании
            // участие завершаетс€ системно.
            // LEFT и EXCLUDED не перезаписываютс€.
            await db.ProjectMemberships
                .Where(x =>
                    x.ProjectId == projectId &&
                    x.Status ==
                        MembershipStatus.ACTIVE)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.Status,
                            MembershipStatus.FINISHED)
                        .SetProperty(
                            x => x.LeftAt,
                            now),
                    ct);
        }

        project.Status =
            nextStatus;

        project.UpdatedAt =
            now;

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return project;
    }

    /// <summary>
    /// Ќазначает или снимает руководител€ проекта.
    ///
    /// ќпераци€ использует ту же блокировку строки проекта,
    /// что и изменение статуса, поэтому две операции
    /// не могут нарушить инвариант:
    /// IN_PROGRESS всегда имеет руководител€.
    /// </summary>
    public async Task<Project> ChangeSupervisorAsync(
        long projectId,
        long? supervisorId,
        CancellationToken ct)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(ct);

        var project =
            await GetLockedProjectAsync(
                projectId,
                ct);

        // –уководител€ выполн€ющегос€ проекта
        // можно заменить, но нельз€ сн€ть полностью.
        if (project.Status ==
                ProjectStatus.IN_PROGRESS &&
            supervisorId is null)
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "PROJECT_SUPERVISOR_CANNOT_BE_REMOVED",
                "Supervisor cannot be removed while the project is in progress.");
        }

        User? supervisor = null;

        if (supervisorId is not null)
        {
            supervisor = await db.Users
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == supervisorId.Value &&
                        x.Role == UserRole.TEACHER,
                    ct);

            if (supervisor is null)
            {
                throw new ApiException(
                    StatusCodes.Status400BadRequest,
                    "INVALID_SUPERVISOR",
                    "Project supervisor must be an existing teacher.");
            }
        }

        // ѕовторное назначение того же руководител€
        // считаетс€ идемпотентной операцией.
        if (project.SupervisorId ==
            supervisor?.Id)
        {
            project.Supervisor =
                supervisor;

            await transaction.CommitAsync(ct);

            return project;
        }

        project.SupervisorId =
            supervisor?.Id;

        project.Supervisor =
            supervisor;

        project.UpdatedAt =
            DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return project;
    }

    /// <summary>
    /// «агружает проект и блокирует его строку
    /// PostgreSQL до окончани€ текущей транзакции.
    /// </summary>
    private async Task<Project> GetLockedProjectAsync(
        long projectId,
        CancellationToken ct)
    {
        var project = await db.Projects
            .FromSqlInterpolated(
                $"SELECT * FROM projects WHERE id = {projectId} FOR UPDATE")
            .SingleOrDefaultAsync(ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "PROJECT_NOT_FOUND",
                "Project not found.");

        // Supervisor загружаетс€ отдельно,
        // чтобы FOR UPDATE примен€лс€ только
        // к строке projects.
        await db.Entry(project)
            .Reference(x => x.Supervisor)
            .LoadAsync(ct);

        return project;
    }

    /// <summary>
    /// ѕровер€ет допустимость перехода
    /// между состо€ни€ми проекта.
    /// </summary>
    private static bool IsAllowedStatusTransition(
        ProjectStatus current,
        ProjectStatus next)
    {
        return current switch
        {
            ProjectStatus.DRAFT =>
                next is ProjectStatus.OPEN
                    or ProjectStatus.ARCHIVED,

            ProjectStatus.OPEN =>
                next is ProjectStatus.DRAFT
                    or ProjectStatus.IN_PROGRESS
                    or ProjectStatus.ARCHIVED,

            ProjectStatus.IN_PROGRESS =>
                next is ProjectStatus.COMPLETED
                    or ProjectStatus.ARCHIVED,

            ProjectStatus.COMPLETED =>
                next is ProjectStatus.ARCHIVED,

            ProjectStatus.ARCHIVED =>
                false,

            _ => false
        };
    }
}