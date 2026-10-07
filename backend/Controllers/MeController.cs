using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Контроллер операций, связанных
/// с текущим авторизованным пользователем.
///
/// Идентификатор пользователя не передаётся frontend'ом,
/// а извлекается из JWT через <c>User.GetUserId()</c>.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me")]
public sealed class MeController(
    AppDbContext db,
    ParticipationApplicationService applications,
    MeProjectService meProject) : ControllerBase
{
    /// <summary>
    /// Возвращает основные данные
    /// текущего авторизованного пользователя.
    /// </summary>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Идентификатор, email, ФИО и роль пользователя.
    /// </returns>
    /// <remarks>
    /// Endpoint: GET /api/v1/me.
    ///
    /// Доступен любому аутентифицированному пользователю.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<CurrentUserResponse>> GetMe(
        CancellationToken ct)
    {
        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == User.GetUserId(),
                ct)
            ?? throw new ApiException(
                StatusCodes.Status401Unauthorized,
                "USER_NOT_FOUND",
                "Authenticated user no longer exists.");

        return Ok(new CurrentUserResponse(
            user.Id,
            user.Email,
            user.FullName,
            user.Role));
    }

    /// <summary>
    /// Возвращает профиль текущего студента.
    /// </summary>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Номер группы, информация о студенте
    /// и его компетенции.
    /// </returns>
    /// <remarks>
    /// Endpoint: GET /api/v1/me/profile.
    ///
    /// Доступен только пользователям с ролью STUDENT.
    ///
    /// Если отдельный StudentProfile ещё не создан,
    /// возвращается объект с пустыми значениями.
    /// </remarks>
    [HttpGet("profile")]
    [Authorize(Roles = nameof(UserRole.STUDENT))]
    public async Task<ActionResult<StudentProfileResponse>> GetProfile(
        CancellationToken ct)
    {
        var profile = await db.StudentProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == User.GetUserId(),
                ct);

        return Ok(new StudentProfileResponse(
            profile?.GroupNumber,
            profile?.About,
            profile?.Competencies));
    }

    /// <summary>
    /// Создаёт или изменяет профиль текущего студента.
    /// </summary>
    /// <param name="request">
    /// Новые данные профиля.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Обновлённый профиль студента.
    /// </returns>
    /// <remarks>
    /// Endpoint: PATCH /api/v1/me/profile.
    ///
    /// Если профиль ещё не существует,
    /// он создаётся автоматически.
    /// </remarks>
    [HttpPatch("profile")]
    [Authorize(Roles = nameof(UserRole.STUDENT))]
    public async Task<ActionResult<StudentProfileResponse>>
        UpdateProfile(
            UpdateStudentProfileRequest request,
            CancellationToken ct)
    {
        var userId = User.GetUserId();

        var profile = await db.StudentProfiles
            .SingleOrDefaultAsync(
                x => x.UserId == userId,
                ct);

        // StudentProfile создаётся лениво:
        // только когда студент впервые сохраняет
        // дополнительные данные.
        if (profile is null)
        {
            profile = new StudentProfile
            {
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            db.StudentProfiles.Add(profile);
        }

        profile.GroupNumber =
            request.GroupNumber?.Trim();

        profile.About =
            request.About;

        profile.Competencies =
            request.Competencies;

        profile.UpdatedAt =
            DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(new StudentProfileResponse(
            profile.GroupNumber,
            profile.About,
            profile.Competencies));
    }

    /// <summary>
    /// Возвращает все заявки текущего студента.
    /// </summary>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// История заявок текущего студента.
    /// </returns>
    /// <remarks>
    /// Endpoint: GET /api/v1/me/applications.
    ///
    /// Доступен только пользователям с ролью STUDENT.
    /// </remarks>
    [HttpGet("applications")]
    [Authorize(Roles = nameof(UserRole.STUDENT))]
    public async Task<ActionResult<
        IReadOnlyList<ApplicationResponse>>> GetApplications(
            CancellationToken ct)
    {
        var applicationsResponse =
            await applications.ListForStudentAsync(
                User.GetUserId(),
                ct);

        return Ok(applicationsResponse);
    }

    /// <summary>
    /// Возвращает активный проект текущего студента.
    /// </summary>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Проект и список его активных участников.
    /// </returns>
    /// <remarks>
    /// Endpoint: GET /api/v1/me/project.
    ///
    /// Проект определяется по ACTIVE ProjectMembership,
    /// а не по заявкам студента.
    /// </remarks>
    [HttpGet("project")]
    [Authorize(Roles = nameof(UserRole.STUDENT))]
    public async Task<ActionResult<MyProjectResponse>> GetProject(
        CancellationToken ct)
    {
        var project = await meProject.GetAsync(
            User.GetUserId(),
            ct);

        return Ok(project);
    }

    /// <summary>
    /// Возвращает проекты,
    /// которыми руководит текущий преподаватель.
    /// </summary>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Список проектов преподавателя
    /// с количеством активных участников.
    /// </returns>
    /// <remarks>
    /// Endpoint: GET /api/v1/me/projects.
    ///
    /// Доступен только пользователям с ролью TEACHER.
    /// В выборку входят только проекты,
    /// у которых SupervisorId равен id текущего пользователя.
    /// </remarks>
    [HttpGet("projects")]
    [Authorize(Roles = nameof(UserRole.TEACHER))]
    public async Task<ActionResult<
        IReadOnlyList<ProjectResponse>>> GetProjects(
            CancellationToken ct)
    {
        var teacherId = User.GetUserId();

        var projects = await db.Projects
            .AsNoTracking()
            .Include(x => x.Supervisor)
            .Where(x =>
                x.SupervisorId == teacherId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        if (projects.Count == 0)
        {
            return Ok(
                Array.Empty<ProjectResponse>());
        }

        var projectIds = projects
            .Select(x => x.Id)
            .ToList();

        // Количество занятых мест не хранится
        // отдельным полем проекта, а вычисляется
        // по ACTIVE ProjectMembership.
        var occupiedPlaces = await db.ProjectMemberships
            .AsNoTracking()
            .Where(x =>
                projectIds.Contains(x.ProjectId) &&
                x.Status == MembershipStatus.ACTIVE)
            .GroupBy(x => x.ProjectId)
            .Select(group => new
            {
                ProjectId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(
                x => x.ProjectId,
                x => x.Count,
                ct);

        var response = projects
            .Select(project =>
                new ProjectResponse(
                    project.Id,
                    project.Code,
                    project.Name,
                    project.Faculty,
                    project.Department,
                    project.Description,
                    project.Goal,
                    project.Direction,
                    project.Semester,
                    project.Competencies ??
                        Array.Empty<string>(),
                    project.Status,
                    project.SupervisorId,
                    project.Supervisor?.FullName,
                    occupiedPlaces.GetValueOrDefault(
                        project.Id),
                    project.TotalPlaces))
            .ToList();

        return Ok(response);
    }
}