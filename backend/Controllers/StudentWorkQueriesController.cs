using AisGpo.Api.Common;
using AisGpo.Api.Contracts.Works;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Чтение работ текущего студента.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.STUDENT))]
[Route("api/v1/me/works")]
public sealed class StudentWorkQueriesController(
    WorkQueryService service) : ControllerBase
{
    /// <summary>
    /// Возвращает список работ текущего студента.
    /// </summary>
    /// <remarks>
    /// GET /api/v1/me/works
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<
        IReadOnlyList<WorkSummaryResponse>>> List(
            CancellationToken ct)
    {
        var result =
            await service.ListForStudentAsync(
                User.GetUserId(),
                ct);

        return Ok(result);
    }

    /// <summary>
    /// Возвращает конкретную работу текущего студента
    /// со всей историей версий.
    /// </summary>
    /// <remarks>
    /// GET /api/v1/me/works/{workId}
    /// </remarks>
    [HttpGet("{workId:long}")]
    public async Task<ActionResult<WorkDetailsResponse>> Get(
        long workId,
        CancellationToken ct)
    {
        var result =
            await service.GetForStudentAsync(
                User.GetUserId(),
                workId,
                ct);

        return Ok(result);
    }
}