using System.Data;
using AisGpo.Api.Common;
using AisGpo.Api.Contracts;
using AisGpo.Api.Data;
using AisGpo.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Services;

/// <summary>
/// Сервис управления заявками студентов
/// на участие в проектах.
///
/// Содержит основную бизнес-логику:
/// создание, отзыв, рассмотрение, одобрение
/// и отклонение заявок.
/// </summary>
public sealed class ParticipationApplicationService(
    AppDbContext db)
{
    /// <summary>
    /// Создаёт новую заявку студента
    /// на участие в проекте.
    /// </summary>
    /// <param name="studentId">
    /// Идентификатор студента.
    /// </param>
    /// <param name="projectId">
    /// Идентификатор проекта.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>Созданная заявка.</returns>
    /// <remarks>
    /// Перед созданием заявки проверяется, что:
    /// студент ещё не участвует в другом проекте,
    /// проект существует и открыт,
    /// в нём есть свободные места,
    /// а у студента отсутствует другая активная
    /// заявка на этот же проект.
    /// </remarks>
    public async Task<ApplicationResponse> CreateAsync(
        long studentId,
        long projectId,
        CancellationToken ct)
    {
        // Студент не может подавать новую заявку,
        // если уже является активным участником проекта.
        if (await db.ProjectMemberships.AnyAsync(
                x =>
                    x.StudentId == studentId &&
                    x.Status == MembershipStatus.ACTIVE,
                ct))
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "STUDENT_ALREADY_HAS_PROJECT",
                "Student already participates in a project.");
        }

        var project = await db.Projects
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "PROJECT_NOT_FOUND",
                "Project not found.");

        // Заявки принимаются только в проекты,
        // находящиеся в статусе OPEN.
        if (project.Status != ProjectStatus.OPEN)
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "PROJECT_NOT_OPEN",
                "Applications can only be submitted to an open project.");
        }

        // Проверяем наличие свободных мест.
        await EnsureProjectHasCapacityAsync(
            project,
            ct);

        // Одновременно разрешена только одна активная
        // заявка студента на конкретный проект.
        if (await db.ParticipationApplications.AnyAsync(
                x =>
                    x.StudentId == studentId &&
                    x.ProjectId == projectId &&
                    (
                        x.Status == ApplicationStatus.CREATED ||
                        x.Status == ApplicationStatus.UNDER_REVIEW
                    ),
                ct))
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "ACTIVE_APPLICATION_ALREADY_EXISTS",
                "Student already has an active application for this project.");
        }

        var student = await db.Users
            .SingleAsync(
                x => x.Id == studentId,
                ct);

        var application =
            new ParticipationApplication
            {
                StudentId = studentId,
                Student = student,
                ProjectId = projectId,
                Project = project,
                Status = ApplicationStatus.CREATED,
                CreatedAt = DateTimeOffset.UtcNow
            };

        db.ParticipationApplications.Add(application);

        // Сохраняем через общий обработчик конфликтов БД,
        // потому что часть ограничений также защищена
        // уникальными индексами PostgreSQL.
        await SaveConflictSafeAsync(ct);

        return Map(application);
    }

    /// <summary>
    /// Возвращает историю заявок конкретного студента.
    /// </summary>
    /// <param name="studentId">
    /// Идентификатор студента.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Список заявок от новых к более старым.
    /// </returns>
    public async Task<IReadOnlyList<ApplicationResponse>>
        ListForStudentAsync(
            long studentId,
            CancellationToken ct)
    {
        var items = await db.ParticipationApplications
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Project)
            .Where(x => x.StudentId == studentId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        return items
            .Select(Map)
            .ToList();
    }

    /// <summary>
    /// Отзывает заявку от имени студента.
    /// </summary>
    /// <param name="id">
    /// Идентификатор заявки.
    /// </param>
    /// <param name="studentId">
    /// Идентификатор текущего студента.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <remarks>
    /// Заявка физически не удаляется из базы,
    /// а переводится в статус CANCELLED.
    ///
    /// Отозвать можно только собственную заявку
    /// в статусе CREATED или UNDER_REVIEW.
    /// </remarks>
    /// <exception cref="ApiException">
    /// Возникает, если заявка не найдена,
    /// принадлежит другому студенту
    /// или уже находится в финальном статусе.
    /// </exception>
    public async Task CancelAsync(
        long id,
        long studentId,
        CancellationToken ct)
    {
        var application =
            await db.ParticipationApplications
                .SingleOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "APPLICATION_NOT_FOUND",
                "Participation application not found.");

        // Студент имеет право отзывать
        // только собственные заявки.
        if (application.StudentId != studentId)
        {
            throw new ApiException(
                StatusCodes.Status403Forbidden,
                "APPLICATION_ACCESS_DENIED",
                "Student can only cancel their own application.");
        }

        // APPROVED, REJECTED и CANCELLED
        // являются финальными состояниями.
        if (application.Status != ApplicationStatus.CREATED &&
            application.Status != ApplicationStatus.UNDER_REVIEW)
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "INVALID_APPLICATION_STATUS",
                "Only CREATED or UNDER_REVIEW application can be cancelled.");
        }

        application.Status =
            ApplicationStatus.CANCELLED;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Возвращает список заявок
    /// для административного интерфейса.
    /// </summary>
    /// <param name="status">
    /// Необязательный фильтр по статусу заявки.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Заявки с информацией о студенте,
    /// проекте и учебной группе.
    /// </returns>
    public async Task<IReadOnlyList<AdminApplicationResponse>>
        ListForAdminAsync(
            ApplicationStatus? status,
            CancellationToken ct)
    {
        var query = db.ParticipationApplications
            .AsNoTracking()
            .Include(x => x.Student)
            .Include(x => x.Project)
            .AsQueryable();

        if (status is not null)
        {
            query = query.Where(
                x => x.Status == status);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

        // StudentProfile хранится отдельно от User,
        // поэтому номера групп загружаются отдельным запросом.
        var studentIds = items
            .Select(x => x.StudentId)
            .Distinct()
            .ToList();

        var profiles = await db.StudentProfiles
            .AsNoTracking()
            .Where(x =>
                studentIds.Contains(x.UserId))
            .ToDictionaryAsync(
                x => x.UserId,
                ct);

        return items
            .Select(x =>
            {
                profiles.TryGetValue(
                    x.StudentId,
                    out var profile);

                return new AdminApplicationResponse(
                    x.Id,
                    x.StudentId,
                    x.Student.FullName,
                    profile?.GroupNumber,
                    x.ProjectId,
                    x.Project.Name,
                    x.Status,
                    x.CreatedAt,
                    x.TakenForReviewAt,
                    x.ReviewedAt,
                    x.RejectionReason);
            })
            .ToList();
    }

    /// <summary>
    /// Переводит новую заявку
    /// в состояние рассмотрения администратором.
    /// </summary>
    /// <param name="id">
    /// Идентификатор заявки.
    /// </param>
    /// <param name="adminId">
    /// Идентификатор администратора,
    /// взявшего заявку на рассмотрение.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Заявка со статусом UNDER_REVIEW.
    /// </returns>
    /// <remarks>
    /// Допустимый переход состояния:
    /// CREATED → UNDER_REVIEW.
    /// </remarks>
    public async Task<ApplicationResponse> TakeForReviewAsync(
        long id,
        long adminId,
        CancellationToken ct)
    {
        var app = await GetTrackedAsync(
            id,
            ct);

        if (app.Status != ApplicationStatus.CREATED)
        {
            throw InvalidStatus(
                "Only CREATED application can be taken for review.");
        }

        app.Status =
            ApplicationStatus.UNDER_REVIEW;

        app.TakenForReviewAt =
            DateTimeOffset.UtcNow;

        app.TakenForReviewById =
            adminId;

        await db.SaveChangesAsync(ct);

        return Map(app);
    }

    /// <summary>
    /// Одобряет заявку студента
    /// на участие в проекте.
    /// </summary>
    /// <param name="id">
    /// Идентификатор заявки.
    /// </param>
    /// <param name="adminId">
    /// Идентификатор администратора,
    /// принимающего решение.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Заявка со статусом APPROVED.
    /// </returns>
    /// <remarks>
    /// При одобрении:
    /// заявка переводится в APPROVED,
    /// создаётся ACTIVE ProjectMembership,
    /// а остальные активные заявки студента отменяются.
    ///
    /// Операция выполняется в транзакции
    /// с уровнем изоляции Serializable.
    /// </remarks>
    public async Task<ApplicationResponse> ApproveAsync(
        long id,
        long adminId,
        CancellationToken ct)
    {
        // Одобрение заявки изменяет сразу несколько сущностей,
        // поэтому вся операция выполняется атомарно.
        await using var tx =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);

        var app = await GetTrackedAsync(
            id,
            ct);

        if (app.Status !=
            ApplicationStatus.UNDER_REVIEW)
        {
            throw InvalidStatus(
                "Only UNDER_REVIEW application can be approved.");
        }

        // Повторно проверяем участие непосредственно
        // перед одобрением, поскольку состояние базы
        // могло измениться после подачи заявки.
        if (await db.ProjectMemberships.AnyAsync(
                x =>
                    x.StudentId == app.StudentId &&
                    x.Status == MembershipStatus.ACTIVE,
                ct))
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "STUDENT_ALREADY_HAS_PROJECT",
                "Student already participates in another project.");
        }

        // Вместимость также проверяется повторно
        // непосредственно в момент одобрения.
        await EnsureProjectHasCapacityAsync(
            app.Project,
            ct);

        app.Status =
            ApplicationStatus.APPROVED;

        app.ReviewedAt =
            DateTimeOffset.UtcNow;

        app.ReviewedById =
            adminId;

        app.RejectionReason =
            null;

        // Только после APPROVED студент становится
        // фактическим участником проекта.
        db.ProjectMemberships.Add(
            new ProjectMembership
            {
                ProjectId = app.ProjectId,
                StudentId = app.StudentId,
                Status = MembershipStatus.ACTIVE,
                JoinedAt = DateTimeOffset.UtcNow
            });

        // После вступления студента в один проект
        // все остальные его незавершённые заявки
        // автоматически отменяются.
        var others =
            await db.ParticipationApplications
                .Where(x =>
                    x.StudentId == app.StudentId &&
                    x.Id != app.Id &&
                    (
                        x.Status == ApplicationStatus.CREATED ||
                        x.Status == ApplicationStatus.UNDER_REVIEW
                    ))
                .ToListAsync(ct);

        foreach (var other in others)
        {
            other.Status =
                ApplicationStatus.CANCELLED;
        }

        await SaveConflictSafeAsync(ct);

        await tx.CommitAsync(ct);

        return Map(app);
    }

    /// <summary>
    /// Отклоняет заявку студента.
    /// </summary>
    /// <param name="id">
    /// Идентификатор заявки.
    /// </param>
    /// <param name="adminId">
    /// Идентификатор администратора,
    /// принимающего решение.
    /// </param>
    /// <param name="reason">
    /// Причина отклонения заявки.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Заявка со статусом REJECTED.
    /// </returns>
    /// <remarks>
    /// Допустимый переход состояния:
    /// UNDER_REVIEW → REJECTED.
    /// </remarks>
    public async Task<ApplicationResponse> RejectAsync(
        long id,
        long adminId,
        string? reason,
        CancellationToken ct)
    {
        var app = await GetTrackedAsync(
            id,
            ct);

        if (app.Status !=
            ApplicationStatus.UNDER_REVIEW)
        {
            throw InvalidStatus(
                "Only UNDER_REVIEW application can be rejected.");
        }

        app.Status =
            ApplicationStatus.REJECTED;

        app.ReviewedAt =
            DateTimeOffset.UtcNow;

        app.ReviewedById =
            adminId;

        app.RejectionReason =
            reason;

        await db.SaveChangesAsync(ct);

        return Map(app);
    }

    /// <summary>
    /// Проверяет наличие свободных мест в проекте.
    /// </summary>
    /// <param name="project">
    /// Проверяемый проект.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <exception cref="ApiException">
    /// Возникает с кодом PROJECT_FULL,
    /// если все места заняты.
    /// </exception>
    private async Task EnsureProjectHasCapacityAsync(
        Project project,
        CancellationToken ct)
    {
        // null означает, что явный лимит мест
        // для проекта не установлен.
        if (project.TotalPlaces is null)
        {
            return;
        }

        var occupiedPlaces =
            await db.ProjectMemberships
                .CountAsync(
                    x =>
                        x.ProjectId == project.Id &&
                        x.Status == MembershipStatus.ACTIVE,
                    ct);

        if (occupiedPlaces >=
            project.TotalPlaces.Value)
        {
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "PROJECT_FULL",
                "Project has no available places.");
        }
    }

    /// <summary>
    /// Загружает заявку для изменения
    /// вместе со связанным студентом и проектом.
    /// </summary>
    /// <param name="id">
    /// Идентификатор заявки.
    /// </param>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    /// <returns>
    /// Отслеживаемая Entity Framework заявка.
    /// </returns>
    private async Task<ParticipationApplication>
        GetTrackedAsync(
            long id,
            CancellationToken ct)
    {
        return await db.ParticipationApplications
            .Include(x => x.Student)
            .Include(x => x.Project)
            .SingleOrDefaultAsync(
                x => x.Id == id,
                ct)
            ?? throw new ApiException(
                StatusCodes.Status404NotFound,
                "APPLICATION_NOT_FOUND",
                "Participation application not found.");
    }

    /// <summary>
    /// Сохраняет изменения в базе данных
    /// и преобразует конфликт ограничений БД
    /// в контролируемую ошибку API.
    /// </summary>
    /// <param name="ct">
    /// Токен отмены асинхронной операции.
    /// </param>
    private async Task SaveConflictSafeAsync(
        CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // В частности, сюда могут попасть нарушения
            // уникальных индексов PostgreSQL.
            throw new ApiException(
                StatusCodes.Status409Conflict,
                "DATA_CONFLICT",
                "Operation conflicts with current data state.");
        }
    }

    /// <summary>
    /// Создаёт стандартную ошибку
    /// некорректного перехода статуса заявки.
    /// </summary>
    /// <param name="message">
    /// Описание недопустимой операции.
    /// </param>
    /// <returns>
    /// Исключение с HTTP 409
    /// и кодом INVALID_APPLICATION_STATUS.
    /// </returns>
    private static ApiException InvalidStatus(
        string message) =>
        new(
            StatusCodes.Status409Conflict,
            "INVALID_APPLICATION_STATUS",
            message);

    /// <summary>
    /// Преобразует сущность заявки
    /// в DTO для передачи через API.
    /// </summary>
    /// <param name="x">
    /// Сущность заявки.
    /// </param>
    /// <returns>
    /// Представление заявки для клиента.
    /// </returns>
    private static ApplicationResponse Map(
        ParticipationApplication x) =>
        new(
            x.Id,
            x.StudentId,
            x.Student.FullName,
            x.ProjectId,
            x.Project.Name,
            x.Status,
            x.CreatedAt,
            x.TakenForReviewAt,
            x.ReviewedAt,
            x.RejectionReason);
}