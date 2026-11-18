using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using AisGpo.Api.Domain.Works;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AisGpo.Api.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public sealed class WorkReviewTests
{
    private readonly IntegrationTestFactory factory;

    public WorkReviewTests(
        IntegrationTestFactory factory)
    {
        this.factory = factory;
    }

    /// <summary>
    /// Преподаватель не может принять работу,
    /// если проект уже не находится в IN_PROGRESS.
    /// </summary>
    [Fact]
    public async Task AcceptWork_WhenProjectIsArchived_ReturnsConflict()
    {
        long teacherId;
        long projectId;
        long workId;
        long revisionId;

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
                        "Archived project with submitted work",

                    Department =
                        "Test department",

                    Status =
                        ProjectStatus.ARCHIVED,

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
                        $"Work {Guid.NewGuid():N}",

                    Status =
                        WorkStatus.SUBMITTED,

                    CreatedAt =
                        now,

                    UpdatedAt =
                        now
                };

            db.StudentWorks.Add(work);

            await db.SaveChangesAsync();

            var revision =
                new WorkRevision
                {
                    StudentWorkId =
                        work.Id,

                    VersionNumber =
                        1,

                    Text =
                        "Integration test work",

                    Status =
                        WorkStatus.SUBMITTED,

                    SubmittedAt =
                        now
                };

            db.WorkRevisions.Add(revision);

            await db.SaveChangesAsync();

            teacherId =
                teacher.Id;

            projectId =
                project.Id;

            workId =
                work.Id;

            revisionId =
                revision.Id;
        }

        using var client =
            factory.CreateAuthenticatedClient(
                teacherId,
                UserRole.TEACHER);

        var response =
            await client.PostAsJsonAsync(
                $"/api/v1/projects/{projectId}/works/{workId}/accept",
                new
                {
                    grade = 5,
                    comment =
                        "Should not be accepted"
                });

        Assert.Equal(
            HttpStatusCode.Conflict,
            response.StatusCode);

        using var json =
            JsonDocument.Parse(
                await response.Content
                    .ReadAsStringAsync());

        Assert.Equal(
            "PROJECT_NOT_IN_PROGRESS",
            json.RootElement
                .GetProperty("code")
                .GetString());

        await using var verificationScope =
            factory.Services.CreateAsyncScope();

        var verificationDb =
            verificationScope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        var persistedWork =
            await verificationDb.StudentWorks
                .AsNoTracking()
                .SingleAsync(
                    x => x.Id == workId);

        var persistedRevision =
            await verificationDb.WorkRevisions
                .AsNoTracking()
                .SingleAsync(
                    x => x.Id == revisionId);

        Assert.Equal(
            WorkStatus.SUBMITTED,
            persistedWork.Status);

        Assert.Equal(
            WorkStatus.SUBMITTED,
            persistedRevision.Status);

        Assert.Null(
            persistedRevision.Grade);

        Assert.Null(
            persistedRevision.ReviewedById);

        Assert.Null(
            persistedRevision.ReviewedAt);
    }
}