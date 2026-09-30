using AisGpo.Api.Common;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.STUDENT))]
[Route("api/v1/applications")]
public sealed class ApplicationsController(
    ParticipationApplicationService service) : ControllerBase
{
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Cancel(
        long id,
        CancellationToken ct)
    {
        await service.CancelAsync(
            id,
            User.GetUserId(),
            ct);

        return NoContent();
    }
}