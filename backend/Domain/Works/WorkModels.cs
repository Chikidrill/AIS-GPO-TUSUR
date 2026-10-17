using AisGpo.Api.Domain;

namespace AisGpo.Api.Domain.Works;

/// <summary>
/// Текущее состояние работы студента.
/// </summary>
public enum WorkStatus
{
    /// <summary>
    /// Работа отправлена студентом
    /// и ожидает проверки преподавателем.
    /// </summary>
    SUBMITTED,

    /// <summary>
    /// Преподаватель вернул работу на доработку.
    /// </summary>
    NEEDS_REVISION,

    /// <summary>
    /// Работа принята преподавателем.
    /// </summary>
    ACCEPTED
}

/// <summary>
/// Работа студента в рамках проекта.
///
/// Содержит общее состояние работы.
/// Каждая фактическая отправка хранится отдельно
/// в <see cref="WorkRevision"/>.
/// </summary>
public sealed class StudentWork
{
    /// <summary>
    /// Уникальный идентификатор работы.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Идентификатор проекта,
    /// в рамках которого выполняется работа.
    /// </summary>
    public long ProjectId { get; set; }

    /// <summary>
    /// Проект, к которому относится работа.
    /// </summary>
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Идентификатор студента — автора работы.
    /// </summary>
    public long StudentId { get; set; }

    /// <summary>
    /// Студент — автор работы.
    /// </summary>
    public User Student { get; set; } = null!;

    /// <summary>
    /// Текущее состояние работы.
    ///
    /// Значение соответствует состоянию
    /// последней версии работы.
    /// </summary>
    public WorkStatus Status { get; set; }

    /// <summary>
    /// Дата создания работы.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Дата последнего изменения работы.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Все версии работы.
    /// </summary>
    public ICollection<WorkRevision> Revisions { get; set; }
        = new List<WorkRevision>();
}

/// <summary>
/// Конкретная версия работы студента.
///
/// При первой отправке создаётся версия 1.
/// После возврата на доработку студент создаёт
/// новую версию, при этом предыдущая сохраняется.
/// </summary>
public sealed class WorkRevision
{
    /// <summary>
    /// Уникальный идентификатор версии.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Идентификатор работы.
    /// </summary>
    public long StudentWorkId { get; set; }

    /// <summary>
    /// Работа, к которой относится версия.
    /// </summary>
    public StudentWork StudentWork { get; set; } = null!;

    /// <summary>
    /// Порядковый номер версии.
    ///
    /// Первая отправка имеет номер 1,
    /// следующая после доработки — 2 и так далее.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Текстовая часть работы.
    ///
    /// Может отсутствовать, если студент
    /// отправил только файлы.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Состояние конкретной версии.
    /// </summary>
    public WorkStatus Status { get; set; }

    /// <summary>
    /// Необязательная оценка преподавателя.
    ///
    /// Конкретную шкалу оценки пока
    /// не ограничиваем на уровне модели.
    /// </summary>
    public int? Grade { get; set; }

    /// <summary>
    /// Комментарий преподавателя.
    ///
    /// При возврате на доработку
    /// комментарий будет обязательным
    /// на уровне бизнес-логики.
    /// </summary>
    public string? ReviewComment { get; set; }

    /// <summary>
    /// Идентификатор преподавателя,
    /// проверившего эту версию.
    /// </summary>
    public long? ReviewedById { get; set; }

    /// <summary>
    /// Преподаватель, проверивший версию.
    /// </summary>
    public User? ReviewedBy { get; set; }

    /// <summary>
    /// Дата отправки версии студентом.
    /// </summary>
    public DateTimeOffset SubmittedAt { get; set; }

    /// <summary>
    /// Дата проверки версии преподавателем.
    /// </summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>
    /// Файлы, приложенные к этой версии.
    /// </summary>
    public ICollection<WorkFile> Files { get; set; }
        = new List<WorkFile>();
}

/// <summary>
/// Метаданные файла,
/// приложенного к версии работы.
///
/// Сам файл хранится в файловом хранилище.
/// В PostgreSQL сохраняется только информация
/// о файле и ключ его расположения.
/// </summary>
public sealed class WorkFile
{
    /// <summary>
    /// Уникальный идентификатор файла.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Идентификатор версии работы.
    /// </summary>
    public long WorkRevisionId { get; set; }

    /// <summary>
    /// Версия работы, к которой относится файл.
    /// </summary>
    public WorkRevision WorkRevision { get; set; } = null!;

    /// <summary>
    /// Исходное имя файла,
    /// которое загрузил пользователь.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Внутренний ключ файла
    /// в файловом хранилище.
    ///
    /// Не совпадает с пользовательским именем файла.
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>
    /// MIME-тип файла.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Размер файла в байтах.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Дата загрузки файла.
    /// </summary>
    public DateTimeOffset UploadedAt { get; set; }
}