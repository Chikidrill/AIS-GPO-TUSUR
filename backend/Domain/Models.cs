namespace AisGpo.Api.Domain;

public enum UserRole { ADMIN, TEACHER, STUDENT }
public enum ProjectStatus { DRAFT, OPEN, IN_PROGRESS, COMPLETED, ARCHIVED }
public enum ApplicationStatus { CREATED, UNDER_REVIEW, APPROVED, REJECTED, CANCELLED }
public enum MembershipStatus { ACTIVE, LEFT, EXCLUDED }

public sealed class User
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{LastName} {FirstName}"
        : $"{LastName} {FirstName} {MiddleName}";
}

public sealed class StudentProfile
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public User User { get; set; } = null!;

    public string? GroupNumber { get; set; }
    public string? About { get; set; }
    public string? Competencies { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Project
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ProjectStatus Status { get; set; }

    public long? SupervisorId { get; set; }
    public User? Supervisor { get; set; }

    public long CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ParticipationApplication
{
    public long Id { get; set; }
    public long StudentId { get; set; }
    public User Student { get; set; } = null!;
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public ApplicationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? TakenForReviewAt { get; set; }
    public long? TakenForReviewById { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public long? ReviewedById { get; set; }
    public string? RejectionReason { get; set; }
}

public sealed class ProjectMembership
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public long StudentId { get; set; }
    public User Student { get; set; } = null!;
    public MembershipStatus Status { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? LeftAt { get; set; }
}

