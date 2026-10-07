using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Контроллер административного управления
/// заявками студентов.
///
/// Все endpoint'ы контроллера доступны
/// только пользователям с ролью ADMIN.
/// </summary>
[ApiController]
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/v1/admin/applications")]
public sealed class AdminApplicationsController(
    ParticipationApplicationService service) : ControllerBase
{
    /// <summary>
    /// Возвращает список заявок для администратора.
    /// </summary>
    /// <param name="status">
    /// Необязательный фильтр по статусу заявки.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Список заявок с данными студента,
    /// группы и проекта.
    /// </returns>
    /// <remarks>
    /// Endpoint: GET /api/v1/admin/applications.
    ///
    /// Например, запрос
    /// <c>?status=UNDER_REVIEW</c>
    /// вернёт только заявки на рассмотрении.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<
        IReadOnlyList<AdminApplicationResponse>>> GetAll(
        [FromQuery] ApplicationStatus? status,
        CancellationToken ct) =>
        Ok(await service.ListForAdminAsync(
            status,
            ct));

    /// <summary>
    /// Берёт новую заявку на рассмотрение.
    /// </summary>
    /// <param name="id">
    /// Идентификатор заявки.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Заявка после перевода в статус UNDER_REVIEW.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// POST /api/v1/admin/applications/{id}/take-for-review.
    ///
    /// Допустимый переход:
    /// CREATED → UNDER_REVIEW.
    ///
    /// Идентификатор администратора
    /// извлекается из JWT.
    /// </remarks>
    [HttpPost("{id:long}/take-for-review")]
    public async Task<ActionResult<ApplicationResponse>>
        TakeForReview(
            long id,
            CancellationToken ct) =>
        Ok(await service.TakeForReviewAsync(
            id,
            User.GetUserId(),
            ct));

    /// <summary>
    /// Одобряет заявку студента.
    /// </summary>
    /// <param name="id">
    /// Идентификатор заявки.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Заявка после перевода в статус APPROVED.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// POST /api/v1/admin/applications/{id}/approve.
    ///
    /// Допустимый переход:
    /// UNDER_REVIEW → APPROVED.
    ///
    /// После одобрения сервис создаёт
    /// ACTIVE ProjectMembership,
    /// поэтому студент становится фактическим
    /// участником проекта.
    /// </remarks>
    [HttpPost("{id:long}/approve")]
    public async Task<ActionResult<ApplicationResponse>> Approve(
        long id,
        CancellationToken ct) =>
        Ok(await service.ApproveAsync(
            id,
            User.GetUserId(),
            ct));

    /// <summary>
    /// Отклоняет заявку студента.
    /// </summary>
    /// <param name="id">
    /// Идентификатор заявки.
    /// </param>
    /// <param name="request">
    /// Данные с причиной отклонения.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Заявка после перевода в статус REJECTED.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// POST /api/v1/admin/applications/{id}/reject.
    ///
    /// Допустимый переход:
    /// UNDER_REVIEW → REJECTED.
    /// </remarks>
    [HttpPost("{id:long}/reject")]
    public async Task<ActionResult<ApplicationResponse>> Reject(
        long id,
        RejectApplicationRequest request,
        CancellationToken ct) =>
        Ok(await service.RejectAsync(
            id,
            User.GetUserId(),
            request.Reason,
            ct));
}