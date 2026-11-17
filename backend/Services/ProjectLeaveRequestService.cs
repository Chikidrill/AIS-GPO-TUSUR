using AisGpo.Api.Common;
using AisGpo.Api.Contracts.LeaveRequests;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AisGpo.Api.Services;

/// <summary>
/// Сервис управления заявками студентов
/// на добровольный выход из проекта.
/// </summary>
public sealed class ProjectLeaveRequestService(
    AppDbContext db)
{
    /// <summary>
    /// Создаёт заявку текущего студента
    /// на выход из его активного проекта.
    /// </summary>
    public async Task<LeaveRequestResponse> CreateAsync(
        long studentId,
        CreateLeaveRequest request,
        CancellationToken ct)
    {
        var membership = await db.ProjectMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.StudentId == studentId &&
                    x.Status == MembershipStatus.ACTIVE,
                ct);

        if (membership is null)
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "MEMBERSHIP_NOT_ACTIVE",
                "Student is not an active participant of a project.");
        }

        var activeRequestExists =
            await db.ProjectLeaveRequests
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.StudentId == studentId &&
                        x.ProjectId == membership.ProjectId &&
                        (
                            x.Status == LeaveRequestStatus.CREATED ||
                            x.Status == LeaveRequestStatus.UNDER_REVIEW
                        ),
                    ct);

        if (activeRequestExists)
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "LEAVE_REQUEST_ALREADY_EXISTS",
                "Student already has an active leave request for this project.");
        }

        var leaveRequest =
            new ProjectLeaveRequest
            {
                StudentId = studentId,
                ProjectId = membership.ProjectId,
                Status = LeaveRequestStatus.CREATED,
                Reason = NormalizeOptionalText(
                    request.Reason),
                CreatedAt = DateTimeOffset.UtcNow
            };

        db.ProjectLeaveRequests.Add(
            leaveRequest);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
            when (
                ex.InnerException is PostgresException
                {
                    SqlState:
                        PostgresErrorCodes.UniqueViolation,
                    ConstraintName:
                        "uq_active_leave_request_per_project"
                })
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "LEAVE_REQUEST_ALREADY_EXISTS",
                "Student already has an active leave request for this project.");
        }

        return await GetResponseAsync(
            leaveRequest.Id,
            ct);
    }

    /// <summary>
    /// Возвращает историю заявок
    /// текущего студента.
    /// </summary>
    public async Task<IReadOnlyList<LeaveRequestResponse>>
        ListForStudentAsync(
            long studentId,
            CancellationToken ct)
    {
        var items = await db.ProjectLeaveRequests
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Project)
            .Where(x =>
                x.StudentId == studentId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items
            .Select(Map)
            .ToList();
    }

    /// <summary>
    /// Отменяет собственную незавершённую
    /// заявку студента.
    /// </summary>
    public async Task CancelAsync(
        long id,
        long studentId,
        CancellationToken ct)
    {
        var snapshot =
            await GetSnapshotAsync(
                id,
                ct);

        if (snapshot.StudentId != studentId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "LEAVE_REQUEST_ACCESS_DENIED",
                "Student can only cancel their own leave request.");
        }

        if (snapshot.Status != LeaveRequestStatus.CREATED &&
            snapshot.Status != LeaveRequestStatus.UNDER_REVIEW)
        {
            throw InvalidStatus(
                "Only CREATED or UNDER_REVIEW leave request can be cancelled.");
        }

        var updated =
            await db.ProjectLeaveRequests
                .Where(x =>
                    x.Id == id &&
                    x.StudentId == studentId &&
                    (
                        x.Status == LeaveRequestStatus.CREATED ||
                        x.Status == LeaveRequestStatus.UNDER_REVIEW
                    ))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.Status,
                            LeaveRequestStatus.CANCELLED),
                    ct);

        if (updated != 1)
        {
            throw InvalidStatus(
                "Leave request status has already changed.");
        }
    }

    /// <summary>
    /// Возвращает заявки для администратора.
    /// </summary>
    public async Task<IReadOnlyList<AdminLeaveRequestResponse>>
        ListForAdminAsync(
            LeaveRequestStatus? status,
            CancellationToken ct)
    {
        var query = db.ProjectLeaveRequests
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Project)
            .AsQueryable();

        if (status is not null)
        {
            query = query.Where(
                x => x.Status == status);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        if (items.Count == 0)
        {
            return Array.Empty<
                AdminLeaveRequestResponse>();
        }

        var studentIds = items
            .Select(x => x.StudentId)
            .Distinct()
            .ToList();

        var profiles = await db.StudentProfiles
            .AsNoTracking()
            .Where(x =>
                studentIds.Contains(x.UserId))
            .ToDictionaryAsync(
                x => x.UserId,
                ct);

        return items
            .Select(x =>
            {
                profiles.TryGetValue(
                    x.StudentId,
                    out var profile);

                return new AdminLeaveRequestResponse(
                    x.Id,
                    x.StudentId,
                    x.Student.FullName,
                    profile?.GroupNumber,
                    x.ProjectId,
                    x.Project.Name,
                    x.Status,
                    x.Reason,
                    x.CreatedAt,
                    x.TakenForReviewAt,
                    x.ReviewedAt,
                    x.RejectionReason);
            })
            .ToList();
    }

    /// <summary>
    /// Переводит CREATED-заявку
    /// в UNDER_REVIEW.
    /// </summary>
    public async Task<LeaveRequestResponse>
        TakeForReviewAsync(
            long id,
            long adminId,
            CancellationToken ct)
    {
        var snapshot =
            await GetSnapshotAsync(
                id,
                ct);

        if (snapshot.Status !=
            LeaveRequestStatus.CREATED)
        {
            throw InvalidStatus(
                "Only CREATED leave request can be taken for review.");
        }

        var now = DateTimeOffset.UtcNow;

        var updated =
            await db.ProjectLeaveRequests
                .Where(x =>
                    x.Id == id &&
                    x.Status ==
                        LeaveRequestStatus.CREATED)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.Status,
                            LeaveRequestStatus.UNDER_REVIEW)
                        .SetProperty(
                            x => x.TakenForReviewAt,
                            now)
                        .SetProperty(
                            x => x.TakenForReviewById,
                            adminId),
                    ct);

        if (updated != 1)
        {
            throw InvalidStatus(
                "Leave request status has already changed.");
        }

        return await GetResponseAsync(
            id,
            ct);
    }

    /// <summary>
    /// Одобряет заявку на выход.
    ///
    /// Заявка переводится в APPROVED,
    /// а ACTIVE membership — в LEFT.
    /// </summary>
    public async Task<LeaveRequestResponse> ApproveAsync(
        long id,
        long adminId,
        CancellationToken ct)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(ct);

        var snapshot =
            await GetSnapshotAsync(
                id,
                ct);

        if (snapshot.Status !=
            LeaveRequestStatus.UNDER_REVIEW)
        {
            throw InvalidStatus(
                "Only UNDER_REVIEW leave request can be approved.");
        }

        var now = DateTimeOffset.UtcNow;

        var requestUpdated =
            await db.ProjectLeaveRequests
                .Where(x =>
                    x.Id == id &&
                    x.Status ==
                        LeaveRequestStatus.UNDER_REVIEW)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.Status,
                            LeaveRequestStatus.APPROVED)
                        .SetProperty(
                            x => x.ReviewedAt,
                            now)
                        .SetProperty(
                            x => x.ReviewedById,
                            adminId)
                        .SetProperty(
                            x => x.RejectionReason,
                            (string?)null),
                    ct);

        if (requestUpdated != 1)
        {
            await transaction.RollbackAsync(ct);

            throw InvalidStatus(
                "Leave request status has already changed.");
        }

        var membershipUpdated =
            await db.ProjectMemberships
                .Where(x =>
                    x.StudentId ==
                        snapshot.StudentId &&
                    x.ProjectId ==
                        snapshot.ProjectId &&
                    x.Status ==
                        MembershipStatus.ACTIVE)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.Status,
                            MembershipStatus.LEFT)
                        .SetProperty(
                            x => x.LeftAt,
                            now),
                    ct);

        if (membershipUpdated != 1)
        {
            await transaction.RollbackAsync(ct);

            throw new ApiException(
                StatusCodes.Status409Conflict,
                "MEMBERSHIP_NOT_ACTIVE",
                "Student is no longer an active participant of this project.");
        }

        await transaction.CommitAsync(ct);

        return await GetResponseAsync(
            id,
            ct);
    }

    /// <summary>
    /// Отклоняет заявку на выход.
    /// Membership при этом не изменяется.
    /// </summary>
    public async Task<LeaveRequestResponse> RejectAsync(
        long id,
        long adminId,
        string? reason,
        CancellationToken ct)
    {
        var snapshot =
            await GetSnapshotAsync(
                id,
                ct);

        if (snapshot.Status !=
            LeaveRequestStatus.UNDER_REVIEW)
        {
            throw InvalidStatus(
                "Only UNDER_REVIEW leave request can be rejected.");
        }

        var now = DateTimeOffset.UtcNow;

        var rejectionReason =
            NormalizeOptionalText(reason);

        var updated =
            await db.ProjectLeaveRequests
                .Where(x =>
                    x.Id == id &&
                    x.Status ==
                        LeaveRequestStatus.UNDER_REVIEW)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            x => x.Status,
                            LeaveRequestStatus.REJECTED)
                        .SetProperty(
                            x => x.ReviewedAt,
                            now)
                        .SetProperty(
                            x => x.ReviewedById,
                            adminId)
                        .SetProperty(
                            x => x.RejectionReason,
                            rejectionReason),
                    ct);

        if (updated != 1)
        {
            throw InvalidStatus(
                "Leave request status has already changed.");
        }

        return await GetResponseAsync(
            id,
            ct);
    }

    /// <summary>
    /// Возвращает минимальные данные заявки,
    /// необходимые для проверки состояния.
    /// </summary>
    private async Task<LeaveRequestSnapshot>
        GetSnapshotAsync(
            long id,
            CancellationToken ct)
    {
        return await db.ProjectLeaveRequests
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x =>
                new LeaveRequestSnapshot(
                    x.StudentId,
                    x.ProjectId,
                    x.Status))
            .SingleOrDefaultAsync(ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "LEAVE_REQUEST_NOT_FOUND",
                "Project leave request not found.");
    }

    /// <summary>
    /// Загружает заявку со связанным
    /// студентом и проектом.
    /// </summary>
    private async Task<LeaveRequestResponse>
        GetResponseAsync(
            long id,
            CancellationToken ct)
    {
        var item = await db.ProjectLeaveRequests
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Project)
            .SingleOrDefaultAsync(
                x => x.Id == id,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "LEAVE_REQUEST_NOT_FOUND",
                "Project leave request not found.");

        return Map(item);
    }

    /// <summary>
    /// Нормализует необязательный текст.
    /// Пустое значение преобразуется в null.
    /// </summary>
    private static string? NormalizeOptionalText(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static ApiException InvalidStatus(
        string message) =>
        new(
            StatusCodes.Status409Conflict,
            "INVALID_LEAVE_REQUEST_STATUS",
            message);

    private static LeaveRequestResponse Map(
        ProjectLeaveRequest x) =>
        new(
            x.Id,
            x.StudentId,
            x.Student.FullName,
            x.ProjectId,
            x.Project.Name,
            x.Status,
            x.Reason,
            x.CreatedAt,
            x.TakenForReviewAt,
            x.ReviewedAt,
            x.RejectionReason);

    private sealed record LeaveRequestSnapshot(
        long StudentId,
        long ProjectId,
        LeaveRequestStatus Status);
}