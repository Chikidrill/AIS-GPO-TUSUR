using AisGpo.Api.Common;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Контроллер операций студента
/// с уже созданными заявками.
///
/// Доступен только пользователям с ролью STUDENT.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.STUDENT))]
[Route("api/v1/applications")]
public sealed class ApplicationsController(
    ParticipationApplicationService service) : ControllerBase
{
    /// <summary>
    /// Отзывает заявку текущего студента.
    /// </summary>
    /// <param name="id">
    /// Идентификатор отзываемой заявки.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// HTTP 204 при успешном отзыве заявки.
    /// </returns>
    /// <remarks>
    /// Endpoint: DELETE /api/v1/applications/{id}.
    ///
    /// Идентификатор текущего студента извлекается из JWT.
    /// Заявка физически не удаляется из базы данных,
    /// а переводится в статус CANCELLED.
    ///
    /// Отозвать можно только собственную заявку
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