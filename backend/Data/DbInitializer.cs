using AisGpo.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Data;

/// <summary>
/// Инициализирует базу данных приложения.
/// Применяет миграции и при включённом DevSeed
/// создаёт тестовых пользователей.
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Применяет миграции и при необходимости
    /// добавляет тестовые данные.
    /// </summary>
    /// <param name="services">
    /// Контейнер зависимостей приложения.
    /// </param>
    /// <param name="configuration">
    /// Конфигурация приложения.
    /// </param>
    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        var db = services.GetRequiredService<AppDbContext>();

        // При запуске приложения применяем
        // все неприменённые EF Core migrations.
        await db.Database.MigrateAsync();

        // Тестовые данные создаются только
        // если DevSeed:Enabled = true.
        if (!configuration.GetValue<bool>("DevSeed:Enabled"))
        {
            return;
        }

        var hasher = new PasswordHasher<User>();

        // Тестовый администратор.
        if (!await db.Users.AnyAsync(
                x => x.Email == "admin@gpo.local"))
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

            admin.PasswordHash = hasher.HashPassword(
                admin,
                "Admin123!");

            db.Users.Add(admin);
        }

        // Тестовый преподаватель.
        if (!await db.Users.AnyAsync(
                x => x.Email == "teacher@gpo.local"))
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

            teacher.PasswordHash = hasher.HashPassword(
                teacher,
                "Teacher123!");

            db.Users.Add(teacher);
        }

        // Тестовый студент.
        if (!await db.Users.AnyAsync(
                x => x.Email == "student@gpo.local"))
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

            student.PasswordHash = hasher.HashPassword(
                student,
                "Student123!");

            db.Users.Add(student);

            // Сначала сохраняем пользователя,
            // чтобы получить сгенерированный student.Id.
            await db.SaveChangesAsync();

            db.StudentProfiles.Add(
                new StudentProfile
                {
                    UserId = student.Id,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
        }

        await db.SaveChangesAsync();
    }
}