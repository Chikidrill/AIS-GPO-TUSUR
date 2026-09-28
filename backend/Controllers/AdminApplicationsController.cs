using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/v1/admin/applications")]
public sealed class AdminApplicationsController(ParticipationApplicationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApplicationResponse>>> GetAll([FromQuery] ApplicationStatus? status, CancellationToken ct) =>
        Ok(await service.ListForAdminAsync(status, ct));

    [HttpPost("{id:long}/take-for-review")]
    public async Task<ActionResult<ApplicationResponse>> TakeForReview(long id, CancellationToken ct) =>
        Ok(await service.TakeForReviewAsync(id, User.GetUserId(), ct));

    [HttpPost("{id:long}/approve")]
    public async Task<ActionResult<ApplicationResponse>> Approve(long id, CancellationToken ct) =>
        Ok(await service.ApproveAsync(id, User.GetUserId(), ct));

    [HttpPost("{id:long}/reject")]
    public async Task<ActionResult<ApplicationResponse>> Reject(long id, RejectApplicationRequest request, CancellationToken ct) =>
        Ok(await service.RejectAsync(id, User.GetUserId(), request.Reason, ct));
}

