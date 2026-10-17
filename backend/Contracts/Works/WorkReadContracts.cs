using AisGpo.Api.Domain.Works;

namespace AisGpo.Api.Contracts.Works;

/// <summary>
/// Краткое представление работы для списков.
/// </summary>
public sealed record WorkSummaryResponse(
    long Id,
    long ProjectId,
    string ProjectName,
    long StudentId,
    string StudentName,
    string? GroupNumber,
    WorkStatus Status,
    int LatestVersionNumber,
    DateTimeOffset LastSubmittedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Полное представление работы.
///
/// В отличие от краткого списка содержит
/// всю историю отправленных версий.
/// </summary>
public sealed record WorkDetailsResponse(
    long Id,
    long ProjectId,
    string ProjectName,
    long StudentId,
    string StudentName,
    string? GroupNumber,
    WorkStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkRevisionResponse> Revisions);