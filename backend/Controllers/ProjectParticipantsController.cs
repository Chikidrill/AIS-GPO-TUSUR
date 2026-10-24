using AisGpo.Api.Common;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Управление участниками проектов.
/// </summary>
[ApiController]
[Authorize(Roles = $"{nameof(UserRole.ADMIN)},{nameof(UserRole.TEACHER)}")]
[Route("api/v1/projects/{projectId:long}/participants")]
public sealed class ProjectParticipantsController(
    ProjectMembershipService service) : ControllerBase
{
    /// <summary>
    /// Исключает активного участника проекта.
    /// Запись об участии сохраняется в истории.
    /// </summary>
    /// <remarks>
    /// POST /api/v1/projects/{projectId}/participants/{studentId}/exclude
    /// </remarks>
    [HttpPost("{studentId:long}/exclude")]
    public async Task<IActionResult> Exclude(
        long projectId,
        long studentId,
        CancellationToken ct)
    {
        var role = GetCurrentRole();

        await service.ExcludeAsync(
            User.GetUserId(),
            role,
            projectId,
            studentId,
            ct);

        return NoContent();
    }

    /// <summary>
    /// Возвращает роль текущего пользователя.
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

        throw new ApiException(
            StatusCodes.Status403Forbidden,
            "MEMBERSHIP_MANAGEMENT_FORBIDDEN",
            "Current user cannot manage project participants.");
    }
}