using AisGpo.Api.Common;
using AisGpo.Api.Contracts.LeaveRequests;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Контроллер заявок текущего студента
/// на добровольный выход из проекта.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.STUDENT))]
[Route("api/v1/me/project/leave-requests")]
public sealed class ProjectLeaveRequestsController(
    ProjectLeaveRequestService service) : ControllerBase
{
    /// <summary>
    /// Создаёт заявку текущего студента
    /// на выход из его активного проекта.
    /// </summary>
    /// <param name="request">
    /// Причина выхода из проекта.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Созданная заявка.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// POST /api/v1/me/project/leave-requests.
    ///
    /// Доступен только STUDENT.
    ///
    /// Студент должен иметь ACTIVE membership.
    /// Одновременно допускается только одна
    /// незавершённая заявка на выход.
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<LeaveRequestResponse>> Create(
        CreateLeaveRequest request,
        CancellationToken ct)
    {
        var result = await service.CreateAsync(
            User.GetUserId(),
            request,
            ct);

        return StatusCode(
            StatusCodes.Status201Created,
            result);
    }

    /// <summary>
    /// Возвращает историю заявок текущего студента
    /// на выход из проектов.
    /// </summary>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Список заявок от новых к старым.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// GET /api/v1/me/project/leave-requests.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<
        IReadOnlyList<LeaveRequestResponse>>> GetAll(
            CancellationToken ct)
    {
        var result =
            await service.ListForStudentAsync(
                User.GetUserId(),
                ct);

        return Ok(result);
    }

    /// <summary>
    /// Отменяет собственную незавершённую
    /// заявку текущего студента.
    /// </summary>
    /// <param name="id">
    /// Идентификатор заявки.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// HTTP 204 при успешной отмене.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// DELETE /api/v1/me/project/leave-requests/{id}.
    ///
    /// Заявка физически не удаляется.
    /// Она переводится в статус CANCELLED.
    ///
    /// Отменить можно только собственную заявку
    /// со статусом CREATED или UNDER_REVIEW.
    /// </remarks>
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