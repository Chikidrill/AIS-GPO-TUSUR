using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Services;

/// <summary>
/// Сервис для получения активного проекта текущего студента.
///
/// Используется для определения,
/// в каком проекте студент фактически участвует,
/// и для получения списка участников этого проекта.
/// </summary>
public sealed class MeProjectService(AppDbContext db)
{
    /// <summary>
    /// Возвращает активный проект студента
    /// вместе со списком его активных участников.
    /// </summary>
    /// <param name="studentId">
    /// Идентификатор текущего студента.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Проект, в котором студент имеет
    /// активное участие, и список его участников.
    /// </returns>
    /// <exception cref="ApiException">
    /// Возникает с кодом ACTIVE_PROJECT_NOT_FOUND,
    /// если студент не состоит ни в одном активном проекте.
    /// </exception>
    public async Task<MyProjectResponse> GetAsync(
        long studentId,
        CancellationToken ct)
    {
        // Ищем ACTIVE membership,
        // а не одобренную заявку.
        // Именно ProjectMembership означает
        // фактическое участие в проекте.
        var membership = await db.ProjectMemberships
            .AsNoTracking()
            .Include(x => x.Project)
            .SingleOrDefaultAsync(
                x =>
                    x.StudentId == studentId &&
                    x.Status == MembershipStatus.ACTIVE,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "ACTIVE_PROJECT_NOT_FOUND",
                "Student is not an active project member.");

        // После определения проекта загружаем
        // всех его текущих ACTIVE участников.
        var rows = await db.ProjectMemberships
            .AsNoTracking()
            .Include(x => x.Student)
            .Where(x =>
                x.ProjectId == membership.ProjectId &&
                x.Status == MembershipStatus.ACTIVE)
            .OrderBy(x => x.JoinedAt)
            .ToListAsync(ct);

        var participants = rows
            .Select(x => new ParticipantResponse(
                x.StudentId,
                x.Student.FullName))
            .ToList();

        return new MyProjectResponse(
            membership.Project.Id,
            membership.Project.Name,
            membership.Project.Description,
            participants);
    }
}