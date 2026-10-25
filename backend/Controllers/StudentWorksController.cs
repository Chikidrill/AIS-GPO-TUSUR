using AisGpo.Api.Common;
using AisGpo.Api.Contracts.Works;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Контроллер отправки работ студентами.
///
/// Доступен только пользователям с ролью STUDENT.
/// Идентификатор студента берётся из JWT.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.STUDENT))]
[Route("api/v1/projects/{projectId:long}/works")]
public sealed class StudentWorksController(
    StudentWorkService service) : ControllerBase
{
    /// <summary>
    /// Отправляет первую версию работы
    /// текущего студента в указанном проекте.
    /// </summary>
    /// <param name="projectId">
    /// Идентификатор проекта.
    /// </param>
    /// <param name="request">
    /// Текст и файлы работы.
    /// </param>
    /// <param name="ct">
    /// Токен отмены операции.
    /// </param>
    /// <returns>
    /// Созданная работа и её первая версия.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// POST /api/v1/projects/{projectId}/works
    ///
    /// Content-Type:
    /// multipart/form-data
    ///
    /// Разрешены три варианта:
    /// только текст,
    /// только файлы,
    /// текст и файлы одновременно.
    ///
    /// Полностью пустая работа запрещена.
    ///
    /// Отправлять работу может только ACTIVE
    /// участник указанного проекта.
    /// </remarks>
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<StudentWorkResponse>> Submit(
        long projectId,
        [FromForm] SubmitWorkRequest request,
        CancellationToken ct)
    {
        var result = await service.SubmitAsync(
            User.GetUserId(),
            projectId,
            request,
            ct);

        return StatusCode(
            StatusCodes.Status201Created,
            result);
    }
}