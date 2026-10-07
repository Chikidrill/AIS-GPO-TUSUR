using System.ComponentModel.DataAnnotations;
using AisGpo.Api.Domain;

namespace AisGpo.Api.Contracts;

/// <summary>
/// Данные, передаваемые пользователем при входе в систему.
/// </summary>
/// <param name="Email">Электронная почта пользователя.</param>
/// <param name="Password">Пароль пользователя.</param>
public sealed record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

/// <summary>
/// Результат успешной аутентификации пользователя.
/// Содержит JWT и основные данные, необходимые frontend для авторизации.
/// </summary>
/// <param name="AccessToken">JWT access token.</param>
/// <param name="TokenType">Тип токена. Для JWT используется Bearer.</param>
/// <param name="ExpiresInSeconds">Время жизни токена в секундах.</param>
/// <param name="UserId">Идентификатор авторизованного пользователя.</param>
/// <param name="Role">Роль авторизованного пользователя.</param>
public sealed record LoginResponse(
    string AccessToken,
    string TokenType,
    long ExpiresInSeconds,
    long UserId,
    UserRole Role);

/// <summary>
/// Краткая информация о текущем авторизованном пользователе.
/// Используется endpoint'ом GET /api/v1/me.
/// </summary>
/// <param name="Id">Идентификатор пользователя.</param>
/// <param name="Email">Электронная почта.</param>
/// <param name="FullName">Полное ФИО.</param>
/// <param name="Role">Роль пользователя.</param>
public sealed record CurrentUserResponse(
    long Id,
    string Email,
    string FullName,
    UserRole Role);

/// <summary>
/// Данные для создания нового проекта администратором.
/// </summary>
/// <param name="Code">Уникальный код проекта.</param>
/// <param name="Name">Название проекта.</param>
/// <param name="Faculty">Факультет или подразделение.</param>
/// <param name="Department">Кафедра.</param>
/// <param name="Description">Описание проекта.</param>
/// <param name="Goal">Цель проекта.</param>
/// <param name="Direction">Направление проекта.</param>
/// <param name="Semester">Семестр реализации проекта.</param>
/// <param name="Competencies">Требуемые компетенции.</param>
/// <param name="TotalPlaces">Максимальное количество участников.</param>
/// <param name="SupervisorId">Идентификатор преподавателя-руководителя.</param>
public sealed record CreateProjectRequest(
    [MaxLength(50)] string? Code,
    [Required, MaxLength(255)] string Name,
    [MaxLength(255)] string? Faculty,
    [Required, MaxLength(255)] string Department,
    [MaxLength(10000)] string? Description,
    [MaxLength(10000)] string? Goal,
    [MaxLength(255)] string? Direction,
    [Range(1, 20)] int? Semester,
    string[]? Competencies,
    [Range(1, int.MaxValue)] int? TotalPlaces,
    long? SupervisorId);

/// <summary>
/// Данные для изменения основной информации о существующем проекте.
///
/// Руководитель и статус проекта изменяются отдельными endpoint'ами.
/// </summary>
/// <param name="Code">Уникальный код проекта.</param>
/// <param name="Name">Название проекта.</param>
/// <param name="Faculty">Факультет или подразделение.</param>
/// <param name="Department">Кафедра.</param>
/// <param name="Description">Описание проекта.</param>
/// <param name="Goal">Цель проекта.</param>
/// <param name="Direction">Направление проекта.</param>
/// <param name="Semester">Семестр реализации проекта.</param>
/// <param name="Competencies">Требуемые компетенции.</param>
/// <param name="TotalPlaces">Максимальное количество участников.</param>
public sealed record UpdateProjectRequest(
    [MaxLength(50)] string? Code,
    [Required, MaxLength(255)] string Name,
    [MaxLength(255)] string? Faculty,
    [Required, MaxLength(255)] string Department,
    [MaxLength(10000)] string? Description,
    [MaxLength(10000)] string? Goal,
    [MaxLength(255)] string? Direction,
    [Range(1, 20)] int? Semester,
    string[]? Competencies,
    [Range(1, int.MaxValue)] int? TotalPlaces);

/// <summary>
/// Представление проекта, возвращаемое через API.
///
/// Помимо сохранённых данных проекта содержит вычисляемое
/// количество активных участников.
/// </summary>
/// <param name="Id">Идентификатор проекта.</param>
/// <param name="Code">Код проекта.</param>
/// <param name="Name">Название проекта.</param>
/// <param name="Faculty">Факультет или подразделение.</param>
/// <param name="Department">Кафедра.</param>
/// <param name="Description">Описание проекта.</param>
/// <param name="Goal">Цель проекта.</param>
/// <param name="Direction">Направление проекта.</param>
/// <param name="Semester">Семестр.</param>
/// <param name="Competencies">Требуемые компетенции.</param>
/// <param name="Status">Текущий статус проекта.</param>
/// <param name="SupervisorId">Идентификатор руководителя.</param>
/// <param name="SupervisorName">ФИО руководителя.</param>
/// <param name="OccupiedPlaces">Количество активных участников.</param>
/// <param name="TotalPlaces">Максимальное количество участников.</param>
public sealed record ProjectResponse(
    long Id,
    string? Code,
    string Name,
    string? Faculty,
    string Department,
    string? Description,
    string? Goal,
    string? Direction,
    int? Semester,
    IReadOnlyList<string> Competencies,
    ProjectStatus Status,
    long? SupervisorId,
    string? SupervisorName,
    int OccupiedPlaces,
    int? TotalPlaces);

/// <summary>
/// Данные для назначения или снятия руководителя проекта.
/// Значение null означает снятие текущего руководителя.
/// </summary>
/// <param name="SupervisorId">
/// Идентификатор пользователя с ролью TEACHER либо null.
/// </param>
public sealed record UpdateProjectSupervisorRequest(
    long? SupervisorId);

/// <summary>
/// Данные для изменения текущего статуса проекта.
/// </summary>
/// <param name="Status">Новый статус проекта.</param>
public sealed record UpdateProjectStatusRequest(
    ProjectStatus Status);

/// <summary>
/// Краткая информация о преподавателе,
/// используемая при выборе руководителя проекта.
/// </summary>
/// <param name="Id">Идентификатор преподавателя.</param>
/// <param name="FullName">Полное ФИО преподавателя.</param>
public sealed record TeacherResponse(
    long Id,
    string FullName);

/// <summary>
/// Профиль текущего студента.
/// </summary>
/// <param name="GroupNumber">Номер учебной группы.</param>
/// <param name="About">Информация о студенте.</param>
/// <param name="Competencies">Компетенции студента.</param>
public sealed record StudentProfileResponse(
    string? GroupNumber,
    string? About,
    string? Competencies);

/// <summary>
/// Данные для изменения профиля текущего студента.
/// </summary>
/// <param name="GroupNumber">Номер учебной группы.</param>
/// <param name="About">Информация о студенте.</param>
/// <param name="Competencies">Компетенции студента.</param>
public sealed record UpdateStudentProfileRequest(
    [MaxLength(50)] string? GroupNumber,
    [MaxLength(4000)] string? About,
    [MaxLength(4000)] string? Competencies);

/// <summary>
/// Представление заявки студента на участие в проекте.
/// Используется в пользовательских и административных операциях.
/// </summary>
/// <param name="Id">Идентификатор заявки.</param>
/// <param name="StudentId">Идентификатор студента.</param>
/// <param name="StudentName">ФИО студента.</param>
/// <param name="ProjectId">Идентификатор проекта.</param>
/// <param name="ProjectName">Название проекта.</param>
/// <param name="Status">Текущий статус заявки.</param>
/// <param name="CreatedAt">Дата создания заявки.</param>
/// <param name="TakenForReviewAt">Дата перевода на рассмотрение.</param>
/// <param name="ReviewedAt">Дата принятия итогового решения.</param>
/// <param name="RejectionReason">Причина отклонения заявки.</param>
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

/// <summary>
/// Расширенное представление заявки для администратора.
///
/// В отличие от <see cref="ApplicationResponse"/>,
/// дополнительно содержит номер учебной группы студента.
/// </summary>
/// <param name="Id">Идентификатор заявки.</param>
/// <param name="StudentId">Идентификатор студента.</param>
/// <param name="StudentName">ФИО студента.</param>
/// <param name="GroupNumber">Номер учебной группы.</param>
/// <param name="ProjectId">Идентификатор проекта.</param>
/// <param name="ProjectName">Название проекта.</param>
/// <param name="Status">Текущий статус заявки.</param>
/// <param name="CreatedAt">Дата создания заявки.</param>
/// <param name="TakenForReviewAt">Дата перевода на рассмотрение.</param>
/// <param name="ReviewedAt">Дата принятия итогового решения.</param>
/// <param name="RejectionReason">Причина отклонения заявки.</param>
public sealed record AdminApplicationResponse(
    long Id,
    long StudentId,
    string StudentName,
    string? GroupNumber,
    long ProjectId,
    string ProjectName,
    ApplicationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? TakenForReviewAt,
    DateTimeOffset? ReviewedAt,
    string? RejectionReason);

/// <summary>
/// Данные, передаваемые администратором при отклонении заявки.
/// </summary>
/// <param name="Reason">Причина отклонения заявки.</param>
public sealed record RejectApplicationRequest(
    [MaxLength(4000)] string? Reason);

/// <summary>
/// Краткая информация об участнике проекта.
/// </summary>
/// <param name="Id">Идентификатор студента.</param>
/// <param name="FullName">Полное ФИО студента.</param>
public sealed record ParticipantResponse(
    long Id,
    string FullName);

/// <summary>
/// Расширенная информация об участнике конкретного проекта,
/// доступная преподавателю-руководителю.
/// </summary>
/// <param name="Id">Идентификатор студента.</param>
/// <param name="FullName">ФИО студента.</param>
/// <param name="GroupNumber">Номер учебной группы.</param>
/// <param name="Competencies">Компетенции студента.</param>
/// <param name="JoinedAt">Дата вступления в проект.</param>
public sealed record ProjectParticipantResponse(
    long Id,
    string FullName,
    string? GroupNumber,
    string? Competencies,
    DateTimeOffset JoinedAt);

/// <summary>
/// Представление активного участника проекта для администратора.
///
/// Содержит данные одновременно о студенте
/// и о проекте, в котором он участвует.
/// </summary>
/// <param name="StudentId">Идентификатор студента.</param>
/// <param name="StudentName">ФИО студента.</param>
/// <param name="GroupNumber">Номер учебной группы.</param>
/// <param name="ProjectId">Идентификатор проекта.</param>
/// <param name="ProjectCode">Код проекта.</param>
/// <param name="ProjectName">Название проекта.</param>
/// <param name="Faculty">Факультет или подразделение.</param>
/// <param name="Department">Кафедра.</param>
/// <param name="JoinedAt">Дата вступления студента в проект.</param>
public sealed record AdminParticipantResponse(
    long StudentId,
    string StudentName,
    string? GroupNumber,
    long ProjectId,
    string? ProjectCode,
    string ProjectName,
    string? Faculty,
    string Department,
    DateTimeOffset JoinedAt);

/// <summary>
/// Представление активного проекта текущего студента.
/// Содержит основные данные проекта и список его участников.
/// </summary>
/// <param name="Id">Идентификатор проекта.</param>
/// <param name="Name">Название проекта.</param>
/// <param name="Description">Описание проекта.</param>
/// <param name="Participants">Список активных участников проекта.</param>
public sealed record MyProjectResponse(
    long Id,
    string Name,
    string? Description,
    IReadOnlyList<ParticipantResponse> Participants);