using System.ComponentModel.DataAnnotations;
using AisGpo.Api.Domain.Works;
using Microsoft.AspNetCore.Http;

namespace AisGpo.Api.Contracts.Works;

/// <summary>
/// Данные для создания новой самостоятельной работы студента.
///
/// Запрос передаётся как multipart/form-data.
/// </summary>
public sealed class CreateWorkRequest
{
    /// <summary>
    /// Название работы.
    /// </summary>
    [Required]
    [MaxLength(255)]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Текстовая часть первой версии работы.
    /// </summary>
    [MaxLength(20000)]
    public string? Text { get; init; }

    /// <summary>
    /// Файлы первой версии работы.
    /// </summary>
    public List<IFormFile> Files { get; init; } = [];
}

/// <summary>
/// Данные для создания новой версии
/// существующей работы после возврата на доработку.
/// </summary>
public sealed class SubmitWorkRevisionRequest
{
    /// <summary>
    /// Текст новой версии работы.
    /// </summary>
    [MaxLength(20000)]
    public string? Text { get; init; }

    /// <summary>
    /// Файлы новой версии работы.
    /// </summary>
    public List<IFormFile> Files { get; init; } = [];
}

/// <summary>
/// Информация о файле,
/// прикреплённом к версии работы.
/// </summary>
public sealed record WorkFileResponse(
    long Id,
    string FileName,
    string ContentType,
    long SizeBytes);

/// <summary>
/// Представление конкретной версии работы.
/// </summary>
public sealed record WorkRevisionResponse(
    long Id,
    int VersionNumber,
    string? Text,
    WorkStatus Status,
    int? Grade,
    string? ReviewComment,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ReviewedAt,
    IReadOnlyList<WorkFileResponse> Files);

/// <summary>
/// Представление работы студента.
/// </summary>
public sealed record StudentWorkResponse(
    long Id,
    string Title,
    long ProjectId,
    string ProjectName,
    long StudentId,
    string StudentName,
    WorkStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    WorkRevisionResponse LatestRevision);