using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/projects")]
public sealed class ProjectsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> GetAll(
        CancellationToken ct)
    {
        var projects = await db.Projects
            .AsNoTracking()
            .Include(x => x.Supervisor)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        if (projects.Count == 0)
            return Ok(Array.Empty<ProjectResponse>());

        var projectIds = projects
            .Select(x => x.Id)
            .ToList();

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
                    occupiedPlaces.GetValueOrDefault(project.Id)))
            .ToList();

        return Ok(response);
    }

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
            return NotFound();

        var occupiedPlaces = await db.ProjectMemberships
            .AsNoTracking()
            .CountAsync(
                x =>
                    x.ProjectId == projectId &&
                    x.Status == MembershipStatus.ACTIVE,
                ct);

        return Ok(MapProject(project, occupiedPlaces));
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public async Task<ActionResult<ProjectResponse>> Create(
        CreateProjectRequest request,
        CancellationToken ct)
    {
        User? supervisor = null;

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
            CreatedById = User.GetUserId(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(
            nameof(GetById),
            new { projectId = project.Id },
            MapProject(
                project,
                occupiedPlaces: 0,
                supervisorOverride: supervisor));
    }

    [HttpGet("{projectId:long}/participants")]
    [Authorize(Roles = nameof(UserRole.TEACHER))]
    public async Task<ActionResult<IReadOnlyList<ProjectParticipantResponse>>> GetParticipants(
        long projectId,
        CancellationToken ct)
    {
        var teacherId = User.GetUserId();

        var project = await db.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                ct);

        if (project is null)
            return NotFound();

        if (project.SupervisorId != teacherId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "PROJECT_ACCESS_DENIED",
                "Teacher is not the supervisor of this project.");
        }

        var memberships = await db.ProjectMemberships
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

        var profiles = await db.StudentProfiles
            .AsNoTracking()
            .Where(x => studentIds.Contains(x.UserId))
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

    private static ProjectResponse MapProject(
        Project project,
        int occupiedPlaces,
        User? supervisorOverride = null)
    {
        var supervisor =
            supervisorOverride ?? project.Supervisor;

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
            project.Competencies ?? Array.Empty<string>(),
            project.Status,
            project.SupervisorId,
            supervisor?.FullName,
            occupiedPlaces,
            project.TotalPlaces);
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}