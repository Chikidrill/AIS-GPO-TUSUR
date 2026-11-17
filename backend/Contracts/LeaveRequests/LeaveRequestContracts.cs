using System.ComponentModel.DataAnnotations;
using AisGpo.Api.Domain;

namespace AisGpo.Api.Contracts.LeaveRequests;

/// <summary>
/// Данные для создания студентом
/// заявки на выход из проекта.
/// </summary>
/// <param name="Reason">
/// Причина добровольного выхода из проекта.
/// Может отсутствовать.
/// </param>
public sealed record CreateLeaveRequest(
    [MaxLength(4000)] string? Reason);

/// <summary>
/// Представление заявки студента
/// на выход из проекта.
/// </summary>
public sealed record LeaveRequestResponse(
    long Id,
    long StudentId,
    string StudentName,
    long ProjectId,
    string ProjectName,
    LeaveRequestStatus Status,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TakenForReviewAt,
    DateTimeOffset? ReviewedAt,
    string? RejectionReason);

/// <summary>
/// Расширенное представление заявки
/// на выход из проекта для администратора.
/// </summary>
public sealed record AdminLeaveRequestResponse(
    long Id,
    long StudentId,
    string StudentName,
    string? GroupNumber,
    long ProjectId,
    string ProjectName,
    LeaveRequestStatus Status,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TakenForReviewAt,
    DateTimeOffset? ReviewedAt,
    string? RejectionReason);

/// <summary>
/// Данные для отклонения заявки
/// на выход из проекта.
/// </summary>
/// <param name="Reason">
/// Причина отклонения.
/// Может отсутствовать.
/// </param>
public sealed record RejectLeaveRequest(
    [MaxLength(4000)] string? Reason);