using AisGpo.Api.Contracts;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.ADMIN))]
[Route("api/v1/admin/teachers")]
public sealed class AdminTeachersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeacherResponse>>> GetAll(
    CancellationToken ct)
    {
        var teachers = await db.Users
            .AsNoTracking()
            .Where(x => x.Role == UserRole.TEACHER)
            .OrderBy(x => x.LastName)
            .ThenBy(x => x.FirstName)
            .ToListAsync(ct);

        var response = teachers
            .Select(x => new TeacherResponse(
                x.Id,
                x.FullName))
            .ToList();

        return Ok(response);
    }
}