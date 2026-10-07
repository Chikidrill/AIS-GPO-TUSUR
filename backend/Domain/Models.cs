namespace AisGpo.Api.Domain;

/// <summary>
/// Роль пользователя в информационной системе.
/// Роль используется для разграничения доступа к API.
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Администратор системы.
    /// Управляет проектами, заявками и участниками.
    /// </summary>
    ADMIN,

    /// <summary>
    /// Преподаватель.
    /// Может быть назначен руководителем проекта.
    /// </summary>
    TEACHER,

    /// <summary>
    /// Студент.
    /// Может просматривать проекты, подавать заявки
    /// и участвовать в одном активном проекте.
    /// </summary>
    STUDENT
}

/// <summary>
/// Состояние проекта в рамках его жизненного цикла.
/// </summary>
public enum ProjectStatus
{
    /// <summary>
    /// Черновик проекта.
    /// </summary>
    DRAFT,

    /// <summary>
    /// Проект открыт для подачи заявок.
    /// </summary>
    OPEN,

    /// <summary>
    /// Проект находится в процессе выполнения.
    /// </summary>
    IN_PROGRESS,

    /// <summary>
    /// Работа над проектом завершена.
    /// </summary>
    COMPLETED,

    /// <summary>
    /// Проект перенесён в архив.
    /// </summary>
    ARCHIVED
}

/// <summary>
/// Состояние заявки студента на участие в проекте.
/// </summary>
public enum ApplicationStatus
{
    /// <summary>
    /// Заявка создана студентом и ожидает обработки.
    /// </summary>
    CREATED,

    /// <summary>
    /// Заявка взята администратором на рассмотрение.
    /// </summary>
    UNDER_REVIEW,

    /// <summary>
    /// Заявка одобрена.
    /// После одобрения студент становится участником проекта.
    /// </summary>
    APPROVED,

    /// <summary>
    /// Заявка отклонена.
    /// </summary>
    REJECTED,

    /// <summary>
    /// Заявка отозвана студентом либо автоматически отменена системой.
    /// </summary>
    CANCELLED
}

/// <summary>
/// Состояние участия студента в проекте.
/// </summary>
public enum MembershipStatus
{
    /// <summary>
    /// Студент является активным участником проекта.
    /// </summary>
    ACTIVE,

    /// <summary>
    /// Студент самостоятельно покинул проект.
    /// </summary>
    LEFT,

    /// <summary>
    /// Студент был исключён из проекта.
    /// </summary>
    EXCLUDED
}

/// <summary>
/// Пользователь информационной системы.
/// Содержит данные для аутентификации, ФИО и роль пользователя.
/// </summary>
public sealed class User
{
    /// <summary>
    /// Уникальный идентификатор пользователя.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Электронная почта пользователя.
    /// Используется в качестве логина при аутентификации.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Хеш пароля пользователя.
    /// Исходный пароль в базе данных не хранится.
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Имя пользователя.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Фамилия пользователя.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Отчество пользователя.
    /// Может отсутствовать.
    /// </summary>
    public string? MiddleName { get; set; }

    /// <summary>
    /// Роль пользователя, определяющая доступные ему операции.
    /// </summary>
    public UserRole Role { get; set; }

    /// <summary>
    /// Дата и время создания пользователя.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Дата и время последнего изменения пользователя.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Возвращает полное ФИО пользователя.
    /// Если отчество отсутствует, возвращаются только фамилия и имя.
    /// </summary>
    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{LastName} {FirstName}"
        : $"{LastName} {FirstName} {MiddleName}";
}

/// <summary>
/// Дополнительный профиль студента.
/// Хранит данные, характерные только для пользователей с ролью STUDENT.
/// </summary>
public sealed class StudentProfile
{
    /// <summary>
    /// Уникальный идентификатор профиля.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Идентификатор пользователя, которому принадлежит профиль.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// Связанный пользователь.
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Номер учебной группы студента.
    /// </summary>
    public string? GroupNumber { get; set; }

    /// <summary>
    /// Дополнительная информация о студенте.
    /// </summary>
    public string? About { get; set; }

    /// <summary>
    /// Компетенции студента в текстовом представлении.
    /// </summary>
    public string? Competencies { get; set; }

    /// <summary>
    /// Дата и время создания профиля.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Дата и время последнего изменения профиля.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Проект группового проектного обучения.
/// Содержит сведения о проекте, его руководителе,
/// вместимости и текущем состоянии.
/// </summary>
public sealed class Project
{
    /// <summary>
    /// Уникальный идентификатор проекта.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Уникальный код проекта.
    /// Может отсутствовать.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Название проекта.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Факультет или подразделение, к которому относится проект.
    /// </summary>
    public string? Faculty { get; set; }

    /// <summary>
    /// Кафедра, ответственная за проект.
    /// </summary>
    public string Department { get; set; } = string.Empty;

    /// <summary>
    /// Подробное описание проекта.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Цель проекта.
    /// </summary>
    public string? Goal { get; set; }

    /// <summary>
    /// Направление проекта.
    /// </summary>
    public string? Direction { get; set; }

    /// <summary>
    /// Семестр реализации проекта.
    /// </summary>
    public int? Semester { get; set; }

    /// <summary>
    /// Список компетенций, необходимых для участия в проекте.
    /// В PostgreSQL хранится как массив text[].
    /// </summary>
    public string[]? Competencies { get; set; }

    /// <summary>
    /// Максимальное количество участников проекта.
    /// </summary>
    public int? TotalPlaces { get; set; }

    /// <summary>
    /// Текущий статус проекта.
    /// </summary>
    public ProjectStatus Status { get; set; }

    /// <summary>
    /// Идентификатор преподавателя — руководителя проекта.
    /// Может отсутствовать, если руководитель ещё не назначен.
    /// </summary>
    public long? SupervisorId { get; set; }

    /// <summary>
    /// Связанный пользователь с ролью TEACHER,
    /// являющийся руководителем проекта.
    /// </summary>
    public User? Supervisor { get; set; }

    /// <summary>
    /// Идентификатор пользователя, создавшего проект.
    /// </summary>
    public long CreatedById { get; set; }

    /// <summary>
    /// Пользователь, создавший проект.
    /// </summary>
    public User CreatedBy { get; set; } = null!;

    /// <summary>
    /// Дата и время создания проекта.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Дата и время последнего изменения проекта.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Заявка студента на участие в проекте.
///
/// Заявка отражает процесс рассмотрения обращения студента
/// и не означает фактическое участие в проекте.
/// Реальное участие представлено сущностью <see cref="ProjectMembership"/>.
/// </summary>
public sealed class ParticipationApplication
{
    /// <summary>
    /// Уникальный идентификатор заявки.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Идентификатор студента, подавшего заявку.
    /// </summary>
    public long StudentId { get; set; }

    /// <summary>
    /// Пользователь-студент, подавший заявку.
    /// </summary>
    public User Student { get; set; } = null!;

    /// <summary>
    /// Идентификатор проекта, на который подана заявка.
    /// </summary>
    public long ProjectId { get; set; }

    /// <summary>
    /// Проект, на который подана заявка.
    /// </summary>
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Текущее состояние заявки.
    /// </summary>
    public ApplicationStatus Status { get; set; }

    /// <summary>
    /// Дата и время создания заявки.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Дата и время перевода заявки на рассмотрение.
    /// </summary>
    public DateTimeOffset? TakenForReviewAt { get; set; }

    /// <summary>
    /// Идентификатор администратора, взявшего заявку на рассмотрение.
    /// </summary>
    public long? TakenForReviewById { get; set; }

    /// <summary>
    /// Дата и время окончательного рассмотрения заявки.
    /// </summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>
    /// Идентификатор администратора,
    /// принявшего окончательное решение по заявке.
    /// </summary>
    public long? ReviewedById { get; set; }

    /// <summary>
    /// Причина отклонения заявки.
    /// Заполняется для заявок со статусом REJECTED.
    /// </summary>
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Фактическое участие студента в проекте.
///
/// Активная запись создаётся после одобрения заявки.
/// В отличие от <see cref="ParticipationApplication"/>,
/// эта сущность показывает, что студент действительно входит в состав проекта.
/// </summary>
public sealed class ProjectMembership
{
    /// <summary>
    /// Уникальный идентификатор участия.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Идентификатор проекта.
    /// </summary>
    public long ProjectId { get; set; }

    /// <summary>
    /// Проект, участником которого является студент.
    /// </summary>
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Идентификатор студента.
    /// </summary>
    public long StudentId { get; set; }

    /// <summary>
    /// Пользователь-студент.
    /// </summary>
    public User Student { get; set; } = null!;

    /// <summary>
    /// Текущее состояние участия студента в проекте.
    /// </summary>
    public MembershipStatus Status { get; set; }

    /// <summary>
    /// Дата и время вступления студента в проект.
    /// </summary>
    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>
    /// Дата и время прекращения участия.
    /// Для активного участия значение отсутствует.
    /// </summary>
    public DateTimeOffset? LeftAt { get; set; }
}