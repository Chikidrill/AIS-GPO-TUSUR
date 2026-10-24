using System.ComponentModel.DataAnnotations;

namespace AisGpo.Api.Contracts.Works;

/// <summary>
/// Данные для принятия работы преподавателем.
/// </summary>
public sealed class AcceptWorkRequest
{
    /// <summary>
    /// Необязательная оценка.
    ///
    /// Шкала оценивания пока не ограничивается,
    /// поскольку она не определена требованиями.
    /// </summary>
    public int? Grade { get; init; }

    /// <summary>
    /// Необязательный комментарий преподавателя.
    /// </summary>
    [MaxLength(4000)]
    public string? Comment { get; init; }
}

/// <summary>
/// Данные для возврата работы на доработку.
/// </summary>
public sealed class RequestWorkRevisionRequest
{
    /// <summary>
    /// Комментарий преподавателя с причиной
    /// возврата работы на доработку.
    ///
    /// Обязательность непустого комментария
    /// дополнительно проверяется в сервисе.
    /// </summary>
    [MaxLength(4000)]
    public string Comment { get; init; } = string.Empty;
}