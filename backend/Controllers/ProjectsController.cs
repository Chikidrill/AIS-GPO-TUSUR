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
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> GetAll(CancellationToken ct)
    {
        var projects = await db.Projects
            .AsNoTracking()
            .Include(x => x.Supervisor)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        var response = projects
            .Select(x => new ProjectResponse(
                x.Id,
                x.Name,
                x.Department,
                x.Description,
                x.Status,
                x.SupervisorId,
                x.Supervisor?.FullName))
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
            .SingleOrDefaultAsync(x => x.Id == projectId, ct);

        if (project is null)
            return NotFound();

        return Ok(new ProjectResponse(
            project.Id,
            project.Name,
            project.Department,
            project.Description,
            project.Status,
            project.SupervisorId,
            project.Supervisor?.FullName));
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
                    x => x.Id == request.SupervisorId.Value
                         && x.Role == UserRole.TEACHER,
                    ct);

            if (supervisor is null)
            {
                throw new ApiException(
                    StatusCodes.Status400BadRequest,
                    "INVALID_SUPERVISOR",
                    "Project supervisor must be an existing teacher.");
            }
        }

        var project = new Project
        {
            Name = request.Name.Trim(),
            Department = request.Department.Trim(),
            Description = request.Description,
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
            new ProjectResponse(
                project.Id,
                project.Name,
                project.Department,
                project.Description,
                project.Status,
                project.SupervisorId,
                supervisor?.FullName));
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
            .SingleOrDefaultAsync(x => x.Id == projectId, ct);

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
            .ToDictionaryAsync(x => x.UserId, ct);

        var response = memberships
            .Select(x =>
            {
                profiles.TryGetValue(x.StudentId, out var profile);

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
}