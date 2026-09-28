using AisGpo.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (!configuration.GetValue<bool>("DevSeed:Enabled"))
            return;

        var hasher = new PasswordHasher<User>();

        if (!await db.Users.AnyAsync(x => x.Email == "admin@gpo.local"))
        {
          var admin = new User
            {
                Email = "admin@gpo.local",
                FirstName = "Администратор",
                LastName = "ГПО",
                Role = UserRole.ADMIN,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            admin.PasswordHash = hasher.HashPassword(admin, "Admin123!");
            db.Users.Add(admin);
        }

        if (!await db.Users.AnyAsync(x => x.Email == "teacher@gpo.local"))
        {
            var teacher = new User
            {
                Email = "teacher@gpo.local",
                FirstName = "Иван",
                LastName = "Иванов",
                MiddleName = "Иванович",
                Role = UserRole.TEACHER,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            teacher.PasswordHash = hasher.HashPassword(teacher, "Teacher123!");
            db.Users.Add(teacher);
        }

        if (!await db.Users.AnyAsync(x => x.Email == "student@gpo.local"))
        {
           var student = new User
            {
                Email = "student@gpo.local",
                FirstName = "Иван",
                LastName = "Студентов",
                Role = UserRole.STUDENT,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            student.PasswordHash = hasher.HashPassword(student, "Student123!");
            db.Users.Add(student);
            await db.SaveChangesAsync();

            db.StudentProfiles.Add(new StudentProfile
            {
                UserId = student.Id,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }
}


