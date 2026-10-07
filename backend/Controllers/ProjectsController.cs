using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Controllers;

/// <summary>
/// Контроллер работы с проектами ГПО.
///
/// Содержит операции чтения каталога,
/// административного управления проектами
/// и получения участников преподавателем.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/projects")]
public sealed class ProjectsController(
    AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Возвращает полный каталог проектов.
    /// </summary>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Список проектов с данными руководителя
    /// и количеством занятых мест.
    /// </returns>
    /// <remarks>
    /// Endpoint: GET /api/v1/projects.
    ///
    /// Доступен любому авторизованному пользователю.
    ///
    /// Количество занятых мест не хранится
    /// отдельным полем проекта, а вычисляется
    /// по ACTIVE ProjectMembership.
    /// </remarks>
    [HttpGet]
    public async Task<ActionResult<
        IReadOnlyList<ProjectResponse>>> GetAll(
            CancellationToken ct)
    {
        var projects = await db.Projects
            .AsNoTracking()
            .Include(x => x.Supervisor)
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

        // Считаем только ACTIVE memberships,
        // поскольку именно они означают
        // фактическое участие в проекте.
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
                MapProject(
                    project,
                    occupiedPlaces.GetValueOrDefault(
                        project.Id)))
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// Возвращает один проект по идентификатору.
    /// </summary>
    /// <param name="projectId">
    /// Идентификатор проекта.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Полное представление проекта.
    /// </returns>
    /// <remarks>
    /// Endpoint: GET /api/v1/projects/{projectId}.
    ///
    /// Вместе с проектом возвращаются
    /// данные руководителя и текущее
    /// количество активных участников.
    /// </remarks>
    [HttpGet("{projectId:long}")]
    public async Task<ActionResult<ProjectResponse>> GetById(
        long projectId,
        CancellationToken ct)
    {
        var project = await db.Projects
            .AsNoTracking()
            .Include(x => x.Supervisor)
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                ct);

        if (project is null)
        {
            return NotFound();
        }

        var occupiedPlaces =
            await db.ProjectMemberships
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.ProjectId == projectId &&
                        x.Status == MembershipStatus.ACTIVE,
                    ct);

        return Ok(
            MapProject(
                project,
                occupiedPlaces));
    }

    /// <summary>
    /// Создаёт новый проект.
    /// </summary>
    /// <param name="request">
    /// Данные создаваемого проекта.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Созданный проект и HTTP 201.
    /// </returns>
    /// <remarks>
    /// Endpoint: POST /api/v1/projects.
    ///
    /// Доступен только ADMIN.
    ///
    /// При создании проверяется:
    /// существование руководителя,
    /// его роль TEACHER,
    /// уникальность кода проекта.
    ///
    /// Новый проект создаётся
    /// со статусом OPEN.
    /// </remarks>
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public async Task<ActionResult<ProjectResponse>> Create(
        CreateProjectRequest request,
        CancellationToken ct)
    {
        User? supervisor = null;

        // Если руководитель указан,
        // он должен существовать
        // и иметь роль TEACHER.
        if (request.SupervisorId is not null)
        {
            supervisor = await db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.SupervisorId.Value &&
                        x.Role == UserRole.TEACHER,
                    ct);

            if (supervisor is null)
            {
                throw new ApiException(
                    StatusCodes.Status400BadRequest,
                    "INVALID_SUPERVISOR",
                    "Project supervisor must be an existing teacher.");
            }
        }

        var code = Normalize(request.Code);

        // Код проекта должен быть уникальным.
        if (code is not null &&
            await db.Projects.AnyAsync(
                x => x.Code == code,
                ct))
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "PROJECT_CODE_ALREADY_EXISTS",
                "Project code already exists.");
        }

        // Нормализуем список компетенций:
        // убираем пробелы, пустые строки
        // и дубликаты без учёта регистра.
        var competencies = request.Competencies?
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var project = new Project
        {
            Code = code,
            Name = request.Name.Trim(),
            Faculty = Normalize(request.Faculty),
            Department = request.Department.Trim(),
            Description = Normalize(request.Description),
            Goal = Normalize(request.Goal),
            Direction = Normalize(request.Direction),
            Semester = request.Semester,
            Competencies = competencies,
            TotalPlaces = request.TotalPlaces,
            SupervisorId = supervisor?.Id,
            Status = ProjectStatus.OPEN,

            // Создатель проекта определяется
            // по uid текущего пользователя в JWT.
            CreatedById = User.GetUserId(),

            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Projects.Add(project);

        await db.SaveChangesAsync(ct);

        return CreatedAtAction(
            nameof(GetById),
            new
            {
                projectId = project.Id
            },
            MapProject(
                project,
                occupiedPlaces: 0,
                supervisorOverride: supervisor));
    }

    /// <summary>
    /// Изменяет основные данные существующего проекта.
    /// </summary>
    /// <param name="projectId">
    /// Идентификатор проекта.
    /// </param>
    /// <param name="request">
    /// Новые данные проекта.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Обновлённый проект.
    /// </returns>
    /// <remarks>
    /// Endpoint: PATCH /api/v1/projects/{projectId}.
    ///
    /// Доступен только ADMIN.
    ///
    /// Руководитель и статус проекта
    /// изменяются отдельными endpoint'ами.
    ///
    /// Нельзя уменьшить TotalPlaces
    /// ниже фактического количества
    /// ACTIVE участников.
    /// </remarks>
    [HttpPatch("{projectId:long}")]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public async Task<ActionResult<ProjectResponse>> Update(
        long projectId,
        UpdateProjectRequest request,
        CancellationToken ct)
    {
        var project = await db.Projects
            .Include(x => x.Supervisor)
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                ct);

        if (project is null)
        {
            return NotFound();
        }

        var code = Normalize(request.Code);

        // Код можно оставить прежним,
        // но нельзя использовать код другого проекта.
        if (code is not null &&
            await db.Projects.AnyAsync(
                x =>
                    x.Id != projectId &&
                    x.Code == code,
                ct))
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "PROJECT_CODE_ALREADY_EXISTS",
                "Project code already exists.");
        }

        if (request.TotalPlaces is not null)
        {
            var occupiedPlaces =
                await db.ProjectMemberships
                    .CountAsync(
                        x =>
                            x.ProjectId == projectId &&
                            x.Status == MembershipStatus.ACTIVE,
                        ct);

            // Нельзя установить вместимость меньше
            // уже существующего количества участников.
            if (request.TotalPlaces.Value <
                occupiedPlaces)
            {
                throw new ApiException(
                    StatusCodes.Status409Conflict,
                    "PROJECT_CAPACITY_TOO_SMALL",
                    "Project capacity cannot be less than the current number of active participants.");
            }
        }

        var competencies = request.Competencies?
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        project.Code = code;
        project.Name = request.Name.Trim();
        project.Faculty =
            Normalize(request.Faculty);
        project.Department =
            request.Department.Trim();
        project.Description =
            Normalize(request.Description);
        project.Goal =
            Normalize(request.Goal);
        project.Direction =
            Normalize(request.Direction);
        project.Semester =
            request.Semester;
        project.Competencies =
            competencies;
        project.TotalPlaces =
            request.TotalPlaces;
        project.UpdatedAt =
            DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        var occupied =
            await db.ProjectMemberships
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.ProjectId == projectId &&
                        x.Status == MembershipStatus.ACTIVE,
                    ct);

        return Ok(
            MapProject(
                project,
                occupied));
    }

    /// <summary>
    /// Назначает или снимает
    /// руководителя проекта.
    /// </summary>
    /// <param name="projectId">
    /// Идентификатор проекта.
    /// </param>
    /// <param name="request">
    /// Новый идентификатор руководителя
    /// либо null для снятия назначения.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Проект после изменения руководителя.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// PATCH /api/v1/projects/{projectId}/supervisor.
    ///
    /// Доступен только ADMIN.
    ///
    /// В качестве руководителя
    /// можно назначить только пользователя
    /// с ролью TEACHER.
    /// </remarks>
    [HttpPatch("{projectId:long}/supervisor")]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public async Task<ActionResult<ProjectResponse>>
        UpdateSupervisor(
            long projectId,
            UpdateProjectSupervisorRequest request,
            CancellationToken ct)
    {
        var project = await db.Projects
            .Include(x => x.Supervisor)
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                ct);

        if (project is null)
        {
            return NotFound();
        }

        User? supervisor = null;

        if (request.SupervisorId is not null)
        {
            supervisor = await db.Users
                .SingleOrDefaultAsync(
                    x =>
                        x.Id == request.SupervisorId.Value &&
                        x.Role == UserRole.TEACHER,
                    ct);

            if (supervisor is null)
            {
                throw new ApiException(
                    StatusCodes.Status400BadRequest,
                    "INVALID_SUPERVISOR",
                    "Project supervisor must be an existing teacher.");
            }
        }

        // null означает снятие руководителя.
        project.SupervisorId =
            supervisor?.Id;

        project.Supervisor =
            supervisor;

        project.UpdatedAt =
            DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        var occupiedPlaces =
            await db.ProjectMemberships
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.ProjectId == projectId &&
                        x.Status == MembershipStatus.ACTIVE,
                    ct);

        return Ok(
            MapProject(
                project,
                occupiedPlaces,
                supervisor));
    }

    /// <summary>
    /// Изменяет текущий статус проекта.
    /// </summary>
    /// <param name="projectId">
    /// Идентификатор проекта.
    /// </param>
    /// <param name="request">
    /// Новый статус проекта.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Проект с обновлённым статусом.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// PATCH /api/v1/projects/{projectId}/status.
    ///
    /// Доступен только ADMIN.
    ///
    /// Поддерживаются статусы:
    /// DRAFT, OPEN, IN_PROGRESS,
    /// COMPLETED и ARCHIVED.
    /// </remarks>
    [HttpPatch("{projectId:long}/status")]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public async Task<ActionResult<ProjectResponse>>
        UpdateStatus(
            long projectId,
            UpdateProjectStatusRequest request,
            CancellationToken ct)
    {
        var project = await db.Projects
            .Include(x => x.Supervisor)
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                ct);

        if (project is null)
        {
            return NotFound();
        }

        project.Status =
            request.Status;

        project.UpdatedAt =
            DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        var occupiedPlaces =
            await db.ProjectMemberships
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.ProjectId == projectId &&
                        x.Status == MembershipStatus.ACTIVE,
                    ct);

        return Ok(
            MapProject(
                project,
                occupiedPlaces));
    }

    /// <summary>
    /// Удаляет проект.
    /// </summary>
    /// <param name="projectId">
    /// Идентификатор проекта.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// HTTP 204 при успешном удалении.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// DELETE /api/v1/projects/{projectId}.
    ///
    /// Доступен только ADMIN.
    ///
    /// Проект разрешается физически удалить
    /// только в том случае, если у него
    /// отсутствуют связанные заявки
    /// и записи об участии.
    ///
    /// При наличии зависимостей возвращается
    /// PROJECT_HAS_DEPENDENCIES.
    /// </remarks>
    [HttpDelete("{projectId:long}")]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public async Task<IActionResult> Delete(
        long projectId,
        CancellationToken ct)
    {
        var project = await db.Projects
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                ct);

        if (project is null)
        {
            return NotFound();
        }

        // Проверяются все заявки,
        // включая исторические.
        var hasApplications =
            await db.ParticipationApplications
                .AnyAsync(
                    x =>
                        x.ProjectId == projectId,
                    ct);

        // Аналогично проверяется вся история
        // участия студентов в проекте.
        var hasMemberships =
            await db.ProjectMemberships
                .AnyAsync(
                    x =>
                        x.ProjectId == projectId,
                    ct);

        if (hasApplications ||
            hasMemberships)
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "PROJECT_HAS_DEPENDENCIES",
                "Project cannot be deleted because it has applications or participants.");
        }

        db.Projects.Remove(project);

        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Возвращает активных участников проекта
    /// для преподавателя-руководителя.
    /// </summary>
    /// <param name="projectId">
    /// Идентификатор проекта.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Список активных участников
    /// с группой и компетенциями.
    /// </returns>
    /// <remarks>
    /// Endpoint:
    /// GET /api/v1/projects/{projectId}/participants.
    ///
    /// Доступен только TEACHER.
    ///
    /// Одной роли TEACHER недостаточно:
    /// текущий преподаватель должен быть
    /// руководителем именно этого проекта.
    /// </remarks>
    [HttpGet("{projectId:long}/participants")]
    [Authorize(Roles = nameof(UserRole.TEACHER))]
    public async Task<ActionResult<
        IReadOnlyList<ProjectParticipantResponse>>>
        GetParticipants(
            long projectId,
            CancellationToken ct)
    {
        var teacherId =
            User.GetUserId();

        var project = await db.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                ct);

        if (project is null)
        {
            return NotFound();
        }

        // Дополнительная объектная авторизация:
        // преподаватель получает доступ
        // только к собственному проекту.
        if (project.SupervisorId != teacherId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "PROJECT_ACCESS_DENIED",
                "Teacher is not the supervisor of this project.");
        }

        var memberships =
            await db.ProjectMemberships
                .AsNoTracking()
                .Include(x => x.Student)
                .Where(x =>
                    x.ProjectId == projectId &&
                    x.Status == MembershipStatus.ACTIVE)
                .OrderBy(x => x.JoinedAt)
                .ToListAsync(ct);

        var studentIds = memberships
            .Select(x => x.StudentId)
            .ToList();

        var profiles =
            await db.StudentProfiles
                .AsNoTracking()
                .Where(x =>
                    studentIds.Contains(x.UserId))
                .ToDictionaryAsync(
                    x => x.UserId,
                    ct);

        var response = memberships
            .Select(x =>
            {
                profiles.TryGetValue(
                    x.StudentId,
                    out var profile);

                return new ProjectParticipantResponse(
                    x.StudentId,
                    x.Student.FullName,
                    profile?.GroupNumber,
                    profile?.Competencies,
                    x.JoinedAt);
            })
            .ToList();

        return Ok(response);
    }

    /// <summary>
    /// Преобразует сущность проекта
    /// в DTO для передачи через API.
    /// </summary>
    /// <param name="project">
    /// Сущность проекта.
    /// </param>
    /// <param name="occupiedPlaces">
    /// Количество активных участников.
    /// </param>
    /// <param name="supervisorOverride">
    /// Необязательный объект руководителя,
    /// используемый вместо navigation property.
    /// </param>
    /// <returns>
    /// Представление проекта для клиента.
    /// </returns>
    private static ProjectResponse MapProject(
        Project project,
        int occupiedPlaces,
        User? supervisorOverride = null)
    {
        var supervisor =
            supervisorOverride ??
            project.Supervisor;

        return new ProjectResponse(
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
            supervisor?.FullName,
            occupiedPlaces,
            project.TotalPlaces);
    }

    /// <summary>
    /// Нормализует необязательное строковое значение.
    /// </summary>
    /// <param name="value">
    /// Исходная строка.
    /// </param>
    /// <returns>
    /// null для пустой или пробельной строки,
    /// иначе строку без внешних пробелов.
    /// </returns>
    private static string? Normalize(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}