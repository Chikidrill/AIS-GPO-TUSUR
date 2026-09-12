using System.Data;
using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Services;

public sealed class ParticipationApplicationService(AppDbContext db)
{
    public async Task<ApplicationResponse> CreateAsync(long studentId, long projectId, CancellationToken ct)
    {
        if (await db.ProjectMemberships.AnyAsync(x => x.StudentId == studentId && x.Status == MembershipStatus.ACTIVE, ct))
            throw new ApiException(StatusCodes.Status409Conflict, "STUDENT_ALREADY_HAS_PROJECT", "Student already participates in a project.");

        var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == projectId, ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "PROJECT_NOT_FOUND", "Project not found.");

        if (project.Status != ProjectStatus.OPEN)
            throw new ApiException(StatusCodes.Status409Conflict, "PROJECT_NOT_OPEN", "Applications can only be submitted to an open project.");

        if (await db.ParticipationApplications.AnyAsync(x =>
            x.StudentId == studentId && x.ProjectId == projectId &&
            (x.Status == ApplicationStatus.CREATED || x.Status == ApplicationStatus.UNDER_REVIEW), ct))
            throw new ApiException(StatusCodes.Status409Conflict, "ACTIVE_APPLICATION_ALREADY_EXISTS", "Student already has an active application for this project.");

        var student = await db.Users.SingleAsync(x => x.Id == studentId, ct);
        var application = new ParticipationApplication
        {
            StudentId = studentId,
            Student = student,
            ProjectId = projectId,
            Project = project,
            Status = ApplicationStatus.CREATED,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.ParticipationApplications.Add(application);
        await SaveConflictSafeAsync(ct);
        return Map(application);
    }

    public async Task<IReadOnlyList<ApplicationResponse>> ListForStudentAsync(long studentId, CancellationToken ct)
    {
        var items = await db.ParticipationApplications.AsNoTracking()
            .Include(x => x.Student).Include(x => x.Project)
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<ApplicationResponse>> ListForAdminAsync(ApplicationStatus? status, CancellationToken ct)
    {
        var query = db.ParticipationApplications.AsNoTracking()
            .Include(x => x.Student).Include(x => x.Project).AsQueryable();
        if (status is not null) query = query.Where(x => x.Status == status);
        var items = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<ApplicationResponse> TakeForReviewAsync(long id, long adminId, CancellationToken ct)
    {
        var app = await GetTrackedAsync(id, ct);
        if (app.Status != ApplicationStatus.CREATED)
            throw InvalidStatus("Only CREATED application can be taken for review.");
        app.Status = ApplicationStatus.UNDER_REVIEW;
        app.TakenForReviewAt = DateTimeOffset.UtcNow;
        app.TakenForReviewById = adminId;
        await db.SaveChangesAsync(ct);
        return Map(app);
    }

    public async Task<ApplicationResponse> ApproveAsync(long id, long adminId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var app = await GetTrackedAsync(id, ct);
        if (app.Status != ApplicationStatus.UNDER_REVIEW)
            throw InvalidStatus("Only UNDER_REVIEW application can be approved.");

        if (await db.ProjectMemberships.AnyAsync(x => x.StudentId == app.StudentId && x.Status == MembershipStatus.ACTIVE, ct))
            throw new ApiException(StatusCodes.Status409Conflict, "STUDENT_ALREADY_HAS_PROJECT", "Student already participates in another project.");

        app.Status = ApplicationStatus.APPROVED;
        app.ReviewedAt = DateTimeOffset.UtcNow;
        app.ReviewedById = adminId;
        app.RejectionReason = null;

        db.ProjectMemberships.Add(new ProjectMembership
        {
            ProjectId = app.ProjectId,
            StudentId = app.StudentId,
            Status = MembershipStatus.ACTIVE,
            JoinedAt = DateTimeOffset.UtcNow
        });

        var others = await db.ParticipationApplications.Where(x =>
            x.StudentId == app.StudentId && x.Id != app.Id &&
            (x.Status == ApplicationStatus.CREATED || x.Status == ApplicationStatus.UNDER_REVIEW)).ToListAsync(ct);
        foreach (var other in others) other.Status = ApplicationStatus.CANCELLED;

        await SaveConflictSafeAsync(ct);
        await tx.CommitAsync(ct);
        return Map(app);
    }

    public async Task<ApplicationResponse> RejectAsync(long id, long adminId, string? reason, CancellationToken ct)
    {
        var app = await GetTrackedAsync(id, ct);
        if (app.Status != ApplicationStatus.UNDER_REVIEW)
            throw InvalidStatus("Only UNDER_REVIEW application can be rejected.");
        app.Status = ApplicationStatus.REJECTED;
        app.ReviewedAt = DateTimeOffset.UtcNow;
        app.ReviewedById = adminId;
        app.RejectionReason = reason;
        await db.SaveChangesAsync(ct);
        return Map(app);
    }

    private async Task<ParticipationApplication> GetTrackedAsync(long id, CancellationToken ct) =>
        await db.ParticipationApplications.Include(x => x.Student).Include(x => x.Project)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new ApiException(StatusCodes.Status404NotFound, "APPLICATION_NOT_FOUND", "Participation application not found.");

    private async Task SaveConflictSafeAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "DATA_CONFLICT", "Operation conflicts with current data state.");
        }
    }

    private static ApiException InvalidStatus(string message) =>
        new(StatusCodes.Status409Conflict, "INVALID_APPLICATION_STATUS", message);

    private static ApplicationResponse Map(ParticipationApplication x) => new(
        x.Id, x.StudentId, x.Student.FullName, x.ProjectId, x.Project.Name, x.Status,
        x.CreatedAt, x.TakenForReviewAt, x.ReviewedAt, x.RejectionReason);
}

