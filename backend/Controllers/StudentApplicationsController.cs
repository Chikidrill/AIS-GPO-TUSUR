using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Контроллер подачи заявок студентом на проекты.
///
/// Доступен только пользователям с ролью STUDENT.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.STUDENT))]
[Route("api/v1/projects/{projectId:long}/applications")]
public sealed class StudentApplicationsController(
    ParticipationApplicationService service) : ControllerBase
{
    /// <summary>
    /// Создаёт заявку текущего студента
    /// на участие в указанном проекте.
    /// </summary>
    /// <param name="projectId">
    /// Идентификатор проекта.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Созданная заявка и HTTP 201.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// POST /api/v1/projects/{projectId}/applications.
    ///
    /// Идентификатор студента берётся из JWT,
    /// поэтому frontend не может указать другого пользователя.
    ///
    /// Основные бизнес-проверки выполняются
    /// в <see cref="ParticipationApplicationService"/>:
    /// наличие активного проекта, статус проекта,
    /// свободные места и существующая активная заявка.
    /// </remarks>
    [HttpPost]
    public async Task<ActionResult<ApplicationResponse>> Create(
        long projectId,
        CancellationToken ct)
    {
        var result = await service.CreateAsync(
            User.GetUserId(),
            projectId,
            ct);

        return StatusCode(
            StatusCodes.Status201Created,
            result);
    }
}