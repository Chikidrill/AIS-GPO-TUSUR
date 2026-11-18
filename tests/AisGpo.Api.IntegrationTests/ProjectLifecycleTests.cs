using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AisGpo.Api.Domain.Works;

namespace AisGpo.Api.IntegrationTests;

/// <summary>
/// Интеграционные тесты жизненного цикла проекта.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ProjectLifecycleTests
{
    private readonly IntegrationTestFactory factory;

    public ProjectLifecycleTests(
        IntegrationTestFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// OPEN-проект без руководителя
    /// нельзя перевести в IN_PROGRESS.
    /// </summary>
    [Fact]
    public async Task StartProject_WithoutSupervisor_ReturnsConflict()
    {
        long adminId;
        long projectId;

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
                        "Integration",

                    LastName =
                        "Admin",

                    Role =
                        UserRole.ADMIN,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            db.Users.Add(admin);

            await db.SaveChangesAsync();

            var project =
                new Project
                {
                    Code =
                        $"TEST-{Guid.NewGuid():N}",

                    Name =
                        "Lifecycle test project",

                    Department =
                        "Test department",

                    Status =
                        ProjectStatus.OPEN,

                    SupervisorId =
                        null,

                    CreatedById =
                        admin.Id,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            db.Projects.Add(project);

            await db.SaveChangesAsync();

            adminId =
                admin.Id;

            projectId =
                project.Id;
        }

        using var client =
            factory.CreateAuthenticatedClient(
                adminId,
                UserRole.ADMIN);

        var response =
            await client.PatchAsJsonAsync(
                $"/api/v1/projects/{projectId}/status",
                new
                {
                    status = "IN_PROGRESS"
                });

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        using var json =
            JsonDocument.Parse(
                await response.Content
                    .ReadAsStringAsync());

        Assert.Equal(
            "PROJECT_SUPERVISOR_REQUIRED",
            json.RootElement
                .GetProperty("code")
                .GetString());

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

        Assert.Equal(
            ProjectStatus.OPEN,
            persistedProject.Status);

        Assert.Null(
            persistedProject.SupervisorId);
    }
    /// <summary>
    /// У IN_PROGRESS-проекта нельзя полностью
    /// снять назначенного руководителя.
    /// </summary>
    [Fact]
    public async Task RemoveSupervisor_FromInProgressProject_ReturnsConflict()
    {
        long adminId;
        long teacherId;
        long projectId;

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
                        "Integration",

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
                        "Integration",

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

            var project =
                new Project
                {
                    Code =
                        $"TEST-{Guid.NewGuid():N}",

                    Name =
                        "In progress lifecycle test",

                    Department =
                        "Test department",

                    Status =
                        ProjectStatus.IN_PROGRESS,

                    SupervisorId =
                        teacher.Id,

                    CreatedById =
                        admin.Id,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            db.Projects.Add(project);

            await db.SaveChangesAsync();

            adminId =
                admin.Id;

            teacherId =
                teacher.Id;

            projectId =
                project.Id;
        }

        using var client =
            factory.CreateAuthenticatedClient(
                adminId,
                UserRole.ADMIN);

        var response =
            await client.PatchAsJsonAsync(
                $"/api/v1/projects/{projectId}/supervisor",
                new
                {
                    supervisorId =
                        (long?)null
                });

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        using var json =
            JsonDocument.Parse(
                await response.Content
                    .ReadAsStringAsync());

        Assert.Equal(
            "PROJECT_SUPERVISOR_CANNOT_BE_REMOVED",
            json.RootElement
                .GetProperty("code")
                .GetString());

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

        Assert.Equal(
            ProjectStatus.IN_PROGRESS,
            persistedProject.Status);

        Assert.Equal(
            teacherId,
            persistedProject.SupervisorId);
    }
    /// <summary>
    /// OPEN-проект с назначенным руководителем
    /// успешно переводится в IN_PROGRESS.
    /// </summary>
    [Fact]
    public async Task StartProject_WithSupervisor_ChangesStatusToInProgress()
    {
        long adminId;
        long teacherId;
        long projectId;

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
                        "Integration",

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
                        "Integration",

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

            var project =
                new Project
                {
                    Code =
                        $"TEST-{Guid.NewGuid():N}",

                    Name =
                        "Project ready to start",

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
                };

            db.Projects.Add(project);

            await db.SaveChangesAsync();

            adminId =
                admin.Id;

            teacherId =
                teacher.Id;

            projectId =
                project.Id;
        }

        using var client =
            factory.CreateAuthenticatedClient(
                adminId,
                UserRole.ADMIN);

        var response =
            await client.PatchAsJsonAsync(
                $"/api/v1/projects/{projectId}/status",
                new
                {
                    status = "IN_PROGRESS"
                });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

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

        Assert.Equal(
            ProjectStatus.IN_PROGRESS,
            persistedProject.Status);

        Assert.Equal(
            teacherId,
            persistedProject.SupervisorId);
    }
    /// <summary>
    /// IN_PROGRESS-проект нельзя завершить,
    /// пока существует SUBMITTED-работа.
    /// </summary>
    [Fact]
    public async Task CompleteProject_WithSubmittedWork_ReturnsConflict()
    {
        long adminId;
        long projectId;
        long workId;

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
                        "Integration",

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
                        "Integration",

                    LastName =
                        "Teacher",

                    Role =
                        UserRole.TEACHER,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            var student =
                new User
                {
                    Email =
                        $"student-{Guid.NewGuid():N}@tests.local",

                    PasswordHash =
                        "not-used",

                    FirstName =
                        "Integration",

                    LastName =
                        "Student",

                    Role =
                        UserRole.STUDENT,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            db.Users.AddRange(
                admin,
                teacher,
                student);

            await db.SaveChangesAsync();

            var project =
                new Project
                {
                    Code =
                        $"TEST-{Guid.NewGuid():N}",

                    Name =
                        "Project with unfinished work",

                    Department =
                        "Test department",

                    Status =
                        ProjectStatus.IN_PROGRESS,

                    SupervisorId =
                        teacher.Id,

                    CreatedById =
                        admin.Id,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            db.Projects.Add(project);

            await db.SaveChangesAsync();

            var work =
                new StudentWork
                {
                    ProjectId =
                        project.Id,

                    StudentId =
                        student.Id,

                    Title =
                        $"Submitted work {Guid.NewGuid():N}",

                    Status =
                        WorkStatus.SUBMITTED,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            db.StudentWorks.Add(work);

            await db.SaveChangesAsync();

            adminId =
                admin.Id;

            projectId =
                project.Id;

            workId =
                work.Id;
        }

        using var client =
            factory.CreateAuthenticatedClient(
                adminId,
                UserRole.ADMIN);

        var response =
            await client.PatchAsJsonAsync(
                $"/api/v1/projects/{projectId}/status",
                new
                {
                    status = "COMPLETED"
                });

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        using var json =
            JsonDocument.Parse(
                await response.Content
                    .ReadAsStringAsync());

        Assert.Equal(
            "PROJECT_HAS_UNFINISHED_WORKS",
            json.RootElement
                .GetProperty("code")
                .GetString());

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

        var persistedWork =
            await verificationDb.StudentWorks
                .AsNoTracking()
                .SingleAsync(
                    x => x.Id == workId);

        Assert.Equal(
            ProjectStatus.IN_PROGRESS,
            persistedProject.Status);

        Assert.Equal(
            WorkStatus.SUBMITTED,
            persistedWork.Status);
    }
    /// <summary>
    /// При успешном завершении проекта
    /// активное участие завершается,
    /// а незавершённая заявка на выход отменяется.
    /// </summary>
    [Fact]
    public async Task CompleteProject_FinishesMembership_AndCancelsLeaveRequest()
    {
        long adminId;
        long projectId;
        long membershipId;
        long leaveRequestId;

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
                        "Integration",

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
                        "Integration",

                    LastName =
                        "Teacher",

                    Role =
                        UserRole.TEACHER,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            var student =
                new User
                {
                    Email =
                        $"student-{Guid.NewGuid():N}@tests.local",

                    PasswordHash =
                        "not-used",

                    FirstName =
                        "Integration",

                    LastName =
                        "Student",

                    Role =
                        UserRole.STUDENT,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            db.Users.AddRange(
                admin,
                teacher,
                student);

            await db.SaveChangesAsync();

            var project =
                new Project
                {
                    Code =
                        $"TEST-{Guid.NewGuid():N}",

                    Name =
                        "Project ready for completion",

                    Department =
                        "Test department",

                    Status =
                        ProjectStatus.IN_PROGRESS,

                    SupervisorId =
                        teacher.Id,

                    CreatedById =
                        admin.Id,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            db.Projects.Add(project);

            await db.SaveChangesAsync();

            var membership =
                new ProjectMembership
                {
                    ProjectId =
                        project.Id,

                    StudentId =
                        student.Id,

                    Status =
                        MembershipStatus.ACTIVE,

                    JoinedAt =
                        now,

                    LeftAt =
                        null
                };

            var leaveRequest =
                new ProjectLeaveRequest
                {
                    ProjectId =
                        project.Id,

                    StudentId =
                        student.Id,

                    Status =
                        LeaveRequestStatus.CREATED,

                    Reason =
                        "Integration test leave request",

                    CreatedAt =
                        now
                };

            db.ProjectMemberships.Add(
                membership);

            db.ProjectLeaveRequests.Add(
                leaveRequest);

            await db.SaveChangesAsync();

            adminId =
                admin.Id;

            projectId =
                project.Id;

            membershipId =
                membership.Id;

            leaveRequestId =
                leaveRequest.Id;
        }

        using var client =
            factory.CreateAuthenticatedClient(
                adminId,
                UserRole.ADMIN);

        var response =
            await client.PatchAsJsonAsync(
                $"/api/v1/projects/{projectId}/status",
                new
                {
                    status = "COMPLETED"
                });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

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

        var persistedMembership =
            await verificationDb.ProjectMemberships
                .AsNoTracking()
                .SingleAsync(
                    x => x.Id == membershipId);

        var persistedLeaveRequest =
            await verificationDb.ProjectLeaveRequests
                .AsNoTracking()
                .SingleAsync(
                    x => x.Id == leaveRequestId);

        Assert.Equal(
            ProjectStatus.COMPLETED,
            persistedProject.Status);

        Assert.Equal(
            MembershipStatus.FINISHED,
            persistedMembership.Status);

        Assert.NotNull(
            persistedMembership.LeftAt);

        Assert.Equal(
            LeaveRequestStatus.CANCELLED,
            persistedLeaveRequest.Status);
    }
    /// <summary>
    /// При запуске OPEN-проекта
    /// незавершённая заявка на участие
    /// автоматически отменяется.
    /// </summary>
    [Fact]
    public async Task StartProject_CancelsPendingParticipationApplication()
    {
        long adminId;
        long projectId;
        long applicationId;

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
                        "Integration",

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
                        "Integration",

                    LastName =
                        "Teacher",

                    Role =
                        UserRole.TEACHER,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            var student =
                new User
                {
                    Email =
                        $"student-{Guid.NewGuid():N}@tests.local",

                    PasswordHash =
                        "not-used",

                    FirstName =
                        "Integration",

                    LastName =
                        "Student",

                    Role =
                        UserRole.STUDENT,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            db.Users.AddRange(
                admin,
                teacher,
                student);

            await db.SaveChangesAsync();

            var project =
                new Project
                {
                    Code =
                        $"TEST-{Guid.NewGuid():N}",

                    Name =
                        "Project with pending application",

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
                };

            db.Projects.Add(project);

            await db.SaveChangesAsync();

            var application =
                new ParticipationApplication
                {
                    StudentId =
                        student.Id,

                    ProjectId =
                        project.Id,

                    Status =
                        ApplicationStatus.CREATED,

                    CreatedAt =
                        now
                };

            db.ParticipationApplications.Add(
                application);

            await db.SaveChangesAsync();

            adminId =
                admin.Id;

            projectId =
                project.Id;

            applicationId =
                application.Id;
        }

        using var client =
            factory.CreateAuthenticatedClient(
                adminId,
                UserRole.ADMIN);

        var response =
            await client.PatchAsJsonAsync(
                $"/api/v1/projects/{projectId}/status",
                new
                {
                    status = "IN_PROGRESS"
                });

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

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

        var persistedApplication =
            await verificationDb.ParticipationApplications
                .AsNoTracking()
                .SingleAsync(
                    x => x.Id == applicationId);

        Assert.Equal(
            ProjectStatus.IN_PROGRESS,
            persistedProject.Status);

        Assert.Equal(
            ApplicationStatus.CANCELLED,
            persistedApplication.Status);
    }
}