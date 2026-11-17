using AisGpo.Api.Common;
using AisGpo.Api.Contracts.LeaveRequests;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Административное управление
/// заявками студентов на выход из проектов.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/v1/admin/leave-requests")]
public sealed class AdminLeaveRequestsController(
    ProjectLeaveRequestService service) : ControllerBase
{
    /// <summary>
    /// Возвращает заявки на выход из проектов.
    /// </summary>
    /// <remarks>
    /// GET /api/v1/admin/leave-requests
    ///
    /// Можно использовать фильтр:
    /// ?status=CREATED
    /// ?status=UNDER_REVIEW
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<
        IReadOnlyList<AdminLeaveRequestResponse>>> GetAll(
        [FromQuery] LeaveRequestStatus? status,
        CancellationToken ct)
    {
        var result =
            await service.ListForAdminAsync(
                status,
                ct);

        return Ok(result);
    }

    /// <summary>
    /// Берёт заявку на рассмотрение.
    /// </summary>
    /// <remarks>
    /// CREATED → UNDER_REVIEW.
    /// </remarks>
    [HttpPost("{id:long}/take-for-review")]
    public async Task<ActionResult<LeaveRequestResponse>>
        TakeForReview(
            long id,
            CancellationToken ct)
    {
        var result =
            await service.TakeForReviewAsync(
                id,
                User.GetUserId(),
                ct);

        return Ok(result);
    }

    /// <summary>
    /// Одобряет выход студента из проекта.
    ///
    /// UNDER_REVIEW → APPROVED.
    /// ACTIVE membership → LEFT.
    /// </summary>
    [HttpPost("{id:long}/approve")]
    public async Task<ActionResult<LeaveRequestResponse>>
        Approve(
            long id,
            CancellationToken ct)
    {
        var result =
            await service.ApproveAsync(
                id,
                User.GetUserId(),
                ct);

        return Ok(result);
    }

    /// <summary>
    /// Отклоняет заявку на выход.
    ///
    /// UNDER_REVIEW → REJECTED.
    /// Membership остаётся ACTIVE.
    /// </summary>
    [HttpPost("{id:long}/reject")]
    public async Task<ActionResult<LeaveRequestResponse>>
        Reject(
            long id,
            RejectLeaveRequest request,
            CancellationToken ct)
    {
        var result =
            await service.RejectAsync(
                id,
                User.GetUserId(),
                request.Reason,
                ct);

        return Ok(result);
    }
}