using System.ComponentModel.DataAnnotations;
using AisGpo.Api.Domain.Works;
using Microsoft.AspNetCore.Http;

namespace AisGpo.Api.Contracts.Works;

/// <summary>
/// Данные для первой отправки работы студентом.
///
/// Запрос передаётся как multipart/form-data,
/// поскольку одновременно может содержать
/// текст и несколько файлов.
/// </summary>
public sealed class SubmitWorkRequest
{
    /// <summary>
    /// Текстовая часть работы.
    ///
    /// Может отсутствовать, если студент
    /// прикладывает хотя бы один файл.
    /// </summary>
    [MaxLength(20000)]
    public string? Text { get; init; }

    /// <summary>
    /// Файлы, приложенные к работе.
    ///
    /// Может быть пустым, если студент
    /// отправляет текстовую работу.
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
    long ProjectId,
    string ProjectName,
    long StudentId,
    string StudentName,
    WorkStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    WorkRevisionResponse LatestRevision);