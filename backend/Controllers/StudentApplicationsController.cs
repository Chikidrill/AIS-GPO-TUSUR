using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.STUDENT))]
[Route("api/v1/projects/{projectId:long}/applications")]
public sealed class StudentApplicationsController(ParticipationApplicationService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApplicationResponse>> Create(long projectId, CancellationToken ct)
    {
        var result = await service.CreateAsync(User.GetUserId(), projectId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}

