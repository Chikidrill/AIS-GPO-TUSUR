using AisGpo.Api.Common;
using AisGpo.Api.Contracts.Works;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Чтение работ преподавателем-руководителем проекта.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.TEACHER))]
[Route("api/v1/projects/{projectId:long}/works")]
public sealed class TeacherWorksController(
    WorkQueryService service) : ControllerBase
{
    /// <summary>
    /// Возвращает работы студентов указанного проекта.
    /// </summary>
    /// <remarks>
    /// GET /api/v1/projects/{projectId}/works
    ///
    /// Доступ разрешён только руководителю проекта.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<
        IReadOnlyList<WorkSummaryResponse>>> List(
            long projectId,
            CancellationToken ct)
    {
        var result =
            await service.ListForTeacherAsync(
                User.GetUserId(),
                projectId,
                ct);

        return Ok(result);
    }

    /// <summary>
    /// Возвращает конкретную работу
    /// со всей историей версий.
    /// </summary>
    /// <remarks>
    /// GET /api/v1/projects/{projectId}/works/{workId}
    /// </remarks>
    [HttpGet("{workId:long}")]
    public async Task<ActionResult<WorkDetailsResponse>> Get(
        long projectId,
        long workId,
        CancellationToken ct)
    {
        var result =
            await service.GetForTeacherAsync(
                User.GetUserId(),
                projectId,
                workId,
                ct);

        return Ok(result);
    }
}