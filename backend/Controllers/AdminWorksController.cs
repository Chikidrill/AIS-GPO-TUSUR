using AisGpo.Api.Contracts.Works;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Административный read-only доступ
/// к работам студентов.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/v1/admin/works")]
public sealed class AdminWorksController(
    WorkQueryService service) : ControllerBase
{
    /// <summary>
    /// Возвращает все работы студентов.
    /// </summary>
    /// <remarks>
    /// GET /api/v1/admin/works
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<
        IReadOnlyList<WorkSummaryResponse>>> List(
            CancellationToken ct)
    {
        var result =
            await service.ListForAdminAsync(ct);

        return Ok(result);
    }

    /// <summary>
    /// Возвращает конкретную работу
    /// со всей историей версий.
    /// </summary>
    /// <remarks>
    /// GET /api/v1/admin/works/{workId}
    /// </remarks>
    [HttpGet("{workId:long}")]
    public async Task<ActionResult<WorkDetailsResponse>> Get(
        long workId,
        CancellationToken ct)
    {
        var result =
            await service.GetForAdminAsync(
                workId,
                ct);

        return Ok(result);
    }
}