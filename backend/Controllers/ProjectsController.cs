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
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> GetAll(CancellationToken ct) =>
        Ok(await db.Projects.AsNoTracking().OrderByDescending(x => x.CreatedAt)
            .Select(x => new ProjectResponse(x.Id, x.Name, x.Description, x.Status)).ToListAsync(ct));

    [HttpGet("{projectId:long}")]
    public async Task<ActionResult<ProjectResponse>> GetById(long projectId, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().Where(x => x.Id == projectId)
            .Select(x => new ProjectResponse(x.Id, x.Name, x.Description, x.Status)).SingleOrDefaultAsync(ct);
        return project is null ? NotFound() : Ok(project);
    }

    [HttpPost]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public async Task<ActionResult<ProjectResponse>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var project = new Project
        {
            Name = request.Name.Trim(),
            Description = request.Description,
            Status = ProjectStatus.OPEN,
            CreatedById = User.GetUserId(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetById), new { projectId = project.Id },
            new ProjectResponse(project.Id, project.Name, project.Description, project.Status));
    }
}

