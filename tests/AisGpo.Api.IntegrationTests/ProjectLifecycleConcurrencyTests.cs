using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AisGpo.Api.IntegrationTests;

/// <summary>
/// Проверяет конкурентные операции
/// жизненного цикла проекта.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ProjectLifecycleConcurrencyTests
{
    private readonly IntegrationTestFactory factory;

    public ProjectLifecycleConcurrencyTests(
        IntegrationTestFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Одновременный запуск OPEN-проекта
    /// и снятие его руководителя
    /// никогда не должны приводить
    /// к состоянию:
    ///
    /// IN_PROGRESS + SupervisorId == null.
    /// </summary>
    [Fact]
    public async Task StartProject_AndRemoveSupervisor_ConcurrentRequests_PreserveInvariant()
    {
        const int attempts = 10;

        long adminId;
        long teacherId;
        long[] projectIds;

        var now =
            DateTimeOffset.UtcNow;

        await using (var scope =
            factory.Services.CreateAsyncScope())
        {
            var db =
                scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            var admin =
                new User
                {
                    Email =
                        $"admin-{Guid.NewGuid():N}@tests.local",

                    PasswordHash =
                        "not-used",

                    FirstName =
                        "Concurrency",

                    LastName =
                        "Admin",

                    Role =
                        UserRole.ADMIN,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            var teacher =
                new User
                {
                    Email =
                        $"teacher-{Guid.NewGuid():N}@tests.local",

                    PasswordHash =
                        "not-used",

                    FirstName =
                        "Concurrency",

                    LastName =
                        "Teacher",

                    Role =
                        UserRole.TEACHER,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            db.Users.AddRange(
                admin,
                teacher);

            await db.SaveChangesAsync();

            var projects =
                Enumerable.Range(
                        0,
                        attempts)
                    .Select(index =>
                        new Project
                        {
                            Code =
                                $"RACE-{index}-{Guid.NewGuid():N}",

                            Name =
                                $"Concurrency project {index}",

                            Department =
                                "Test department",

                            Status =
                                ProjectStatus.OPEN,

                            SupervisorId =
                                teacher.Id,

                            CreatedById =
                                admin.Id,

                            CreatedAt =
                                now,

                            UpdatedAt =
                                now
                        })
                    .ToList();

            db.Projects.AddRange(
                projects);

            await db.SaveChangesAsync();

            adminId =
                admin.Id;

            teacherId =
                teacher.Id;

            projectIds =
                projects
                    .Select(x => x.Id)
                    .ToArray();
        }

        using var statusClient =
            factory.CreateAuthenticatedClient(
                adminId,
                UserRole.ADMIN);

        using var supervisorClient =
            factory.CreateAuthenticatedClient(
                adminId,
                UserRole.ADMIN);

        foreach (var projectId in projectIds)
        {
            // Оба запроса запускаются без ожидания
            // завершения первого.
            //
            // Таким образом они конкурируют
            // за одну строку projects.
            var statusTask =
                statusClient.PatchAsJsonAsync(
                    $"/api/v1/projects/{projectId}/status",
                    new
                    {
                        status =
                            "IN_PROGRESS"
                    });

            var supervisorTask =
                supervisorClient.PatchAsJsonAsync(
                    $"/api/v1/projects/{projectId}/supervisor",
                    new
                    {
                        supervisorId =
                            (long?)null
                    });

            await Task.WhenAll(
                statusTask,
                supervisorTask);

            using var statusResponse =
                await statusTask;

            using var supervisorResponse =
                await supervisorTask;

            var statusCodes =
                new[]
                {
                    statusResponse.StatusCode,
                    supervisorResponse.StatusCode
                };

            // Ровно одна операция должна пройти,
            // а вторая должна получить бизнес-конфликт.
            Assert.Equal(
                1,
                statusCodes.Count(
                    x => x == HttpStatusCode.OK));

            Assert.Equal(
                1,
                statusCodes.Count(
                    x => x == HttpStatusCode.Conflict));

            HttpResponseMessage conflictResponse;

            if (statusResponse.StatusCode ==
                HttpStatusCode.Conflict)
            {
                conflictResponse =
                    statusResponse;
            }
            else
            {
                conflictResponse =
                    supervisorResponse;
            }

            using var conflictJson =
                JsonDocument.Parse(
                    await conflictResponse.Content
                        .ReadAsStringAsync());

            var conflictCode =
                conflictJson.RootElement
                    .GetProperty("code")
                    .GetString();

            Assert.True(
                conflictCode is
                    "PROJECT_SUPERVISOR_REQUIRED"
                    or
                    "PROJECT_SUPERVISOR_CANNOT_BE_REMOVED",
                $"Unexpected conflict code: {conflictCode}");

            await using var verificationScope =
                factory.Services.CreateAsyncScope();

            var verificationDb =
                verificationScope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

            var persistedProject =
                await verificationDb.Projects
                    .AsNoTracking()
                    .SingleAsync(
                        x => x.Id == projectId);

            // Главный инвариант.
            Assert.False(
                persistedProject.Status ==
                    ProjectStatus.IN_PROGRESS &&
                persistedProject.SupervisorId is null,
                "Project must never be IN_PROGRESS without a supervisor.");

            // Если первым выиграл запуск проекта,
            // снятие руководителя должно быть отклонено.
            if (statusResponse.StatusCode ==
                HttpStatusCode.OK)
            {
                Assert.Equal(
                    ProjectStatus.IN_PROGRESS,
                    persistedProject.Status);

                Assert.Equal(
                    teacherId,
                    persistedProject.SupervisorId);

                Assert.Equal(
                    "PROJECT_SUPERVISOR_CANNOT_BE_REMOVED",
                    conflictCode);
            }
            else
            {
                // Если первым выиграло снятие руководителя,
                // последующий запуск должен быть отклонён.
                Assert.Equal(
                    ProjectStatus.OPEN,
                    persistedProject.Status);

                Assert.Null(
                    persistedProject.SupervisorId);

                Assert.Equal(
                    "PROJECT_SUPERVISOR_REQUIRED",
                    conflictCode);
            }
        }
    }
}