using System.ComponentModel.DataAnnotations;
using AisGpo.Api.Domain;

namespace AisGpo.Api.Contracts;

public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    long ExpiresInSeconds,
    long UserId,
    UserRole Role);

public sealed record CurrentUserResponse(long Id, string Email, string FullName, UserRole Role);

public sealed record CreateProjectRequest(
    [Required, MaxLength(255)] string Name,
    [Required, MaxLength(255)] string Department,
    [MaxLength(10000)] string? Description,
    long? SupervisorId);

public sealed record ProjectResponse(
    long Id,
    string Name,
    string Department,
    string? Description,
    ProjectStatus Status,
    long? SupervisorId,
    string? SupervisorName);

public sealed record StudentProfileResponse(
    string? GroupNumber,
    string? About,
    string? Competencies);

public sealed record UpdateStudentProfileRequest(
    [MaxLength(50)] string? GroupNumber,
    [MaxLength(4000)] string? About,
    [MaxLength(4000)] string? Competencies);

public sealed record ApplicationResponse(
    long Id,
    long StudentId,
    string StudentName,
    long ProjectId,
    string ProjectName,
    ApplicationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TakenForReviewAt,
    DateTimeOffset? ReviewedAt,
    string? RejectionReason);

public sealed record RejectApplicationRequest([MaxLength(4000)] string? Reason);

public sealed record ParticipantResponse(long Id, string FullName);

public sealed record MyProjectResponse(
    long Id,
    string Name,
    string? Description,
    IReadOnlyList<ParticipantResponse> Participants);

