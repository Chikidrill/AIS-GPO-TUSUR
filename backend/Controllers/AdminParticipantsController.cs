using AisGpo.Api.Contracts;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/v1/admin/participants")]
public sealed class AdminParticipantsController(
    AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminParticipantResponse>>> GetAll(
        CancellationToken ct)
    {
        var memberships = await db.ProjectMemberships
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Project)
            .Where(x => x.Status == MembershipStatus.ACTIVE)
            .OrderBy(x => x.Student.LastName)
            .ThenBy(x => x.Student.FirstName)
            .ToListAsync(ct);

        if (memberships.Count == 0)
            return Ok(Array.Empty<AdminParticipantResponse>());

        var studentIds = memberships
            .Select(x => x.StudentId)
            .Distinct()
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

                return new AdminParticipantResponse(
                    x.StudentId,
                    x.Student.FullName,
                    profile?.GroupNumber,
                    x.ProjectId,
                    x.Project.Code,
                    x.Project.Name,
                    x.Project.Faculty,
                    x.Project.Department,
                    x.JoinedAt);
            })
            .ToList();

        return Ok(response);
    }
}