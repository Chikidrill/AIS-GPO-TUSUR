using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Services;

public sealed class MeProjectService(AppDbContext db)
{
    public async Task<MyProjectResponse> GetAsync(long studentId, CancellationToken ct)
    {
        var membership = await db.ProjectMemberships.AsNoTracking().Include(x => x.Project)
            .SingleOrDefaultAsync(x => x.StudentId == studentId && x.Status == MembershipStatus.ACTIVE, ct)
            ?? throw new ApiException(StatusCodes.Status404NotFound, "ACTIVE_PROJECT_NOT_FOUND", "Student is not an active project member.");

        var rows = await db.ProjectMemberships.AsNoTracking().Include(x => x.Student)
            .Where(x => x.ProjectId == membership.ProjectId && x.Status == MembershipStatus.ACTIVE)
            .OrderBy(x => x.JoinedAt)
            .ToListAsync(ct);

        var participants = rows.Select(x => new ParticipantResponse(x.StudentId, x.Student.FullName)).ToList();
        return new MyProjectResponse(
            membership.Project.Id,
            membership.Project.Name,
            membership.Project.Description,
            participants);
    }
}

