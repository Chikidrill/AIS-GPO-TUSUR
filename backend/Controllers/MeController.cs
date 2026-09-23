using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using AisGpo.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/me")]
public sealed class MeController(AppDbContext db, ParticipationApplicationService applications, MeProjectService meProject) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CurrentUserResponse>> GetMe(CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == User.GetUserId(), ct)
            ?? throw new ApiException(StatusCodes.Status401Unauthorized, "USER_NOT_FOUND", "Authenticated user no longer exists.");
        return Ok(new CurrentUserResponse(user.Id, user.Email, user.FullName, user.Role));
    }

    [HttpGet("profile")]
    [Authorize(Roles = nameof(UserRole.STUDENT))]
    public async Task<ActionResult<StudentProfileResponse>> GetProfile(CancellationToken ct)
    {
        var profile = await db.StudentProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == User.GetUserId(), ct);
        return Ok(new StudentProfileResponse(
            profile?.GroupNumber,
            profile?.About,
            profile?.Competencies));
    }

    [HttpPatch("profile")]
    [Authorize(Roles = nameof(UserRole.STUDENT))]
    public async Task<ActionResult<StudentProfileResponse>> UpdateProfile(UpdateStudentProfileRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var profile = await db.StudentProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (profile is null)
        {
            profile = new StudentProfile { UserId = userId, CreatedAt = DateTimeOffset.UtcNow };
            db.StudentProfiles.Add(profile);
        }
        profile.GroupNumber = request.GroupNumber?.Trim();
        profile.About = request.About;
        profile.Competencies = request.Competencies;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(new StudentProfileResponse(
            profile.GroupNumber,
            profile.About,
            profile.Competencies));
    }

    [HttpGet("applications")]
    [Authorize(Roles = nameof(UserRole.STUDENT))]
    public async Task<ActionResult<IReadOnlyList<ApplicationResponse>>> GetApplications(CancellationToken ct) =>
        Ok(await applications.ListForStudentAsync(User.GetUserId(), ct));

    [HttpGet("project")]
    [Authorize(Roles = nameof(UserRole.STUDENT))]
    public async Task<ActionResult<MyProjectResponse>> GetProject(CancellationToken ct) =>
        Ok(await meProject.GetAsync(User.GetUserId(), ct));
    [HttpGet("projects")]
    [Authorize(Roles = nameof(UserRole.TEACHER))]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> GetProjects(
        CancellationToken ct)
    {
        var teacherId = User.GetUserId();

        var projects = await db.Projects
            .AsNoTracking()
            .Include(x => x.Supervisor)
            .Where(x => x.SupervisorId == teacherId)
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
}

