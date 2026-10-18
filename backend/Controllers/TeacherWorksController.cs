using AisGpo.Api.Common;
using AisGpo.Api.Contracts.Works;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Работа преподавателя с работами студентов
/// в проектах, которыми он руководит.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.TEACHER))]
[Route("api/v1/projects/{projectId:long}/works")]
public sealed class TeacherWorksController(
    WorkQueryService queryService,
    WorkReviewService reviewService) : ControllerBase
{
    /// <summary>
    /// Возвращает работы студентов указанного проекта.
    /// </summary>
    /// <remarks>
    /// GET /api/v1/projects/{projectId}/works
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<
        IReadOnlyList<WorkSummaryResponse>>> List(
            long projectId,
            CancellationToken ct)
    {
        var result =
            await queryService.ListForTeacherAsync(
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
            await queryService.GetForTeacherAsync(
                User.GetUserId(),
                projectId,
                workId,
                ct);

        return Ok(result);
    }

    /// <summary>
    /// Принимает последнюю отправленную версию работы.
    /// </summary>
    /// <remarks>
    /// POST /api/v1/projects/{projectId}/works/{workId}/accept
    ///
    /// Оценка и комментарий необязательны.
    /// </remarks>
    [HttpPost("{workId:long}/accept")]
    public async Task<ActionResult<WorkDetailsResponse>> Accept(
        long projectId,
        long workId,
        AcceptWorkRequest request,
        CancellationToken ct)
    {
        var result =
            await reviewService.AcceptAsync(
                User.GetUserId(),
                projectId,
                workId,
                request,
                ct);

        return Ok(result);
    }

    /// <summary>
    /// Возвращает последнюю версию работы
    /// студенту на доработку.
    /// </summary>
    /// <remarks>
    /// POST /api/v1/projects/{projectId}/works/{workId}/request-revision
    ///
    /// Комментарий преподавателя обязателен.
    /// </remarks>
    [HttpPost("{workId:long}/request-revision")]
    public async Task<ActionResult<WorkDetailsResponse>>
        RequestRevision(
            long projectId,
            long workId,
            RequestWorkRevisionRequest request,
            CancellationToken ct)
    {
        var result =
            await reviewService.RequestRevisionAsync(
                User.GetUserId(),
                projectId,
                workId,
                request,
                ct);

        return Ok(result);
    }
}