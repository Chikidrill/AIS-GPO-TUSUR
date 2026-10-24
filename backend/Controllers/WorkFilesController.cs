using AisGpo.Api.Common;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Защищённое скачивание файлов,
/// приложенных к работам студентов.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/work-files")]
public sealed class WorkFilesController(
    WorkFileService service) : ControllerBase
{
    /// <summary>
    /// Возвращает файл работы после проверки доступа.
    /// </summary>
    /// <remarks>
    /// GET /api/v1/work-files/{fileId}
    ///
    /// STUDENT — только собственная работа.
    /// TEACHER — только проект под его руководством.
    /// ADMIN — административный read-only доступ.
    /// </remarks>
    [HttpGet("{fileId:long}")]
    public async Task<IActionResult> Download(
        long fileId,
        CancellationToken ct)
    {
        var role = GetCurrentRole();

        var result = await service.OpenAsync(
            User.GetUserId(),
            role,
            fileId,
            ct);

        return File(
            result.Content,
            result.ContentType,
            result.FileName,
            enableRangeProcessing: true);
    }

    /// <summary>
    /// Определяет роль текущего пользователя
    /// по JWT claims.
    /// </summary>
    private UserRole GetCurrentRole()
    {
        if (User.IsInRole(nameof(UserRole.ADMIN)))
        {
            return UserRole.ADMIN;
        }

        if (User.IsInRole(nameof(UserRole.TEACHER)))
        {
            return UserRole.TEACHER;
        }

        if (User.IsInRole(nameof(UserRole.STUDENT)))
        {
            return UserRole.STUDENT;
        }

        throw new ApiException(
            StatusCodes.Status403Forbidden,
            "ROLE_ACCESS_DENIED",
            "User role is not allowed to access work files.");
    }
}