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
public sealed class MeController(
    AppDbContext db,
    ParticipationApplicationService applications,
    MeProjectService meProject) : ControllerBase
{
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

    [HttpPatch("profile")]
    [Authorize(Roles = nameof(UserRole.STUDENT))]
    public async Task<ActionResult<StudentProfileResponse>> UpdateProfile(
        UpdateStudentProfileRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();

        var profile = await db.StudentProfiles
            .SingleOrDefaultAsync(
                x => x.UserId == userId,
                ct);

        if (profile is null)
        {
            profile = new StudentProfile
            {
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            };

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
    public async Task<ActionResult<IReadOnlyList<ApplicationResponse>>> GetApplications(
        CancellationToken ct)
    {
        var applicationsResponse = await applications
            .ListForStudentAsync(
                User.GetUserId(),
                ct);

        return Ok(applicationsResponse);
    }

    [HttpGet("project")]
    [Authorize(Roles = nameof(UserRole.STUDENT))]
    public async Task<ActionResult<MyProjectResponse>> GetProject(
        CancellationToken ct)
    {
        var project = await meProject
            .GetAsync(
                User.GetUserId(),
                ct);

        return Ok(project);
    }

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
            .Select(project => new ProjectResponse(
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
                project.Supervisor?.FullName,
                occupiedPlaces.GetValueOrDefault(project.Id),
                project.TotalPlaces))
            .ToList();

        return Ok(response);
    }
}