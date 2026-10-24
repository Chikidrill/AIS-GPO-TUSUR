using AisGpo.Api.Common;
using AisGpo.Api.Contracts.Works;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Повторная отправка студенческой работы.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.STUDENT))]
[Route("api/v1/me/works/{workId:long}/revisions")]
public sealed class StudentWorkRevisionsController(
    WorkResubmissionService service) : ControllerBase
{
    /// <summary>
    /// Создаёт новую версию работы
    /// после возврата на доработку.
    /// </summary>
    /// <remarks>
    /// POST /api/v1/me/works/{workId}/revisions
    ///
    /// Работа может содержать текст, файлы
    /// или текст и файлы одновременно.
    /// </remarks>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<WorkDetailsResponse>> Resubmit(
        long workId,
        [FromForm] SubmitWorkRequest request,
        CancellationToken ct)
    {
        var result =
            await service.ResubmitAsync(
                User.GetUserId(),
                workId,
                request,
                ct);

        return Created(
            $"/api/v1/me/works/{workId}",
            result);
    }
}