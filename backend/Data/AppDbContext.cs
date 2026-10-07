using AisGpo.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AisGpo.Api.Data;

/// <summary>
/// Контекст Entity Framework Core приложения.
///
/// Описывает таблицы PostgreSQL, связи между сущностями,
/// ограничения, индексы и преобразование enum-значений.
/// </summary>
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Пользователи системы.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Дополнительные профили студентов.
    /// </summary>
    public DbSet<StudentProfile> StudentProfiles =>
        Set<StudentProfile>();

    /// <summary>
    /// Проекты ГПО.
    /// </summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>
    /// Заявки студентов на участие в проектах.
    /// </summary>
    public DbSet<ParticipationApplication>
        ParticipationApplications =>
        Set<ParticipationApplication>();

    /// <summary>
    /// Записи о фактическом участии студентов в проектах.
    /// </summary>
    public DbSet<ProjectMembership> ProjectMemberships =>
        Set<ProjectMembership>();

    /// <summary>
    /// Настраивает отображение доменных сущностей
    /// на таблицы PostgreSQL.
    /// </summary>
    /// <param name="b">
    /// Построитель модели Entity Framework Core.
    /// </param>
    protected override void OnModelCreating(
        ModelBuilder b)
    {
        // -------------------------------------------------
        // User -> users
        // -------------------------------------------------
        b.Entity<User>(e =>
        {
            e.ToTable("users");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id");

            e.Property(x => x.Email)
                .HasColumnName("email")
                .HasMaxLength(255)
                .IsRequired();

            e.Property(x => x.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(500)
                .IsRequired();

            e.Property(x => x.FirstName)
                .HasColumnName("first_name")
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.LastName)
                .HasColumnName("last_name")
                .HasMaxLength(100)
                .IsRequired();

            e.Property(x => x.MiddleName)
                .HasColumnName("middle_name")
                .HasMaxLength(100);

            // Роли хранятся в PostgreSQL как строки:
            // ADMIN, TEACHER, STUDENT.
            e.Property(x => x.Role)
                .HasColumnName("role")
                .HasConversion<string>()
                .HasMaxLength(20);

            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at");

            e.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at");

            // Две учётные записи не могут
            // использовать один email.
            e.HasIndex(x => x.Email)
                .IsUnique();
        });

        // -------------------------------------------------
        // StudentProfile -> student_profiles
        // -------------------------------------------------
        b.Entity<StudentProfile>(e =>
        {
            e.ToTable("student_profiles");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id");

            e.Property(x => x.UserId)
                .HasColumnName("user_id");

            e.Property(x => x.GroupNumber)
                .HasColumnName("group_number")
                .HasMaxLength(50);

            e.Property(x => x.About)
                .HasColumnName("about");

            e.Property(x => x.Competencies)
                .HasColumnName("competencies");

            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at");

            e.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at");

            // У одного пользователя может быть
            // только один StudentProfile.
            e.HasIndex(x => x.UserId)
                .IsUnique();

            // При удалении пользователя
            // его дополнительный профиль
            // также удаляется.
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // -------------------------------------------------
        // Project -> projects
        // -------------------------------------------------
        b.Entity<Project>(e =>
        {
            e.ToTable("projects");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id");

            e.Property(x => x.Code)
                .HasColumnName("code")
                .HasMaxLength(50);

            e.Property(x => x.Name)
                .HasColumnName("name")
                .HasMaxLength(255)
                .IsRequired();

            e.Property(x => x.Faculty)
                .HasColumnName("faculty")
                .HasMaxLength(255);

            e.Property(x => x.Department)
                .HasColumnName("department")
                .HasMaxLength(255)
                .IsRequired();

            e.Property(x => x.Description)
                .HasColumnName("description");

            e.Property(x => x.Goal)
                .HasColumnName("goal");

            e.Property(x => x.Direction)
                .HasColumnName("direction")
                .HasMaxLength(255);

            e.Property(x => x.Semester)
                .HasColumnName("semester");

            // Компетенции проекта хранятся
            // как массив строк PostgreSQL text[].
            e.Property(x => x.Competencies)
                .HasColumnName("competencies")
                .HasColumnType("text[]");

            e.Property(x => x.TotalPlaces)
                .HasColumnName("total_places");

            // Статус проекта хранится строкой:
            // DRAFT, OPEN, IN_PROGRESS,
            // COMPLETED или ARCHIVED.
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(30);

            e.Property(x => x.SupervisorId)
                .HasColumnName("supervisor_id");

            e.Property(x => x.CreatedById)
                .HasColumnName("created_by");

            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at");

            e.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at");

            // Код проекта уникален
            // на уровне самой базы данных.
            e.HasIndex(x => x.Code)
                .IsUnique();

            // Руководитель проекта связан с User.
            // Restrict запрещает удалить пользователя,
            // пока он связан с проектом как руководитель.
            e.HasOne(x => x.Supervisor)
                .WithMany()
                .HasForeignKey(x => x.SupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            // Аналогично сохраняется ссылка
            // на пользователя, создавшего проект.
            e.HasOne(x => x.CreatedBy)
                .WithMany()
                .HasForeignKey(x => x.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // -------------------------------------------------
        // ParticipationApplication
        // -> participation_applications
        // -------------------------------------------------
        b.Entity<ParticipationApplication>(e =>
        {
            e.ToTable("participation_applications");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id");

            e.Property(x => x.StudentId)
                .HasColumnName("student_id");

            e.Property(x => x.ProjectId)
                .HasColumnName("project_id");

            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(30);

            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at");

            e.Property(x => x.TakenForReviewAt)
                .HasColumnName("taken_for_review_at");

            e.Property(x => x.TakenForReviewById)
                .HasColumnName("taken_for_review_by");

            e.Property(x => x.ReviewedAt)
                .HasColumnName("reviewed_at");

            e.Property(x => x.ReviewedById)
                .HasColumnName("reviewed_by");

            e.Property(x => x.RejectionReason)
                .HasColumnName("rejection_reason");

            e.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            // Критичное бизнес-ограничение:
            // у студента не может одновременно быть
            // две активные заявки на один проект.
            //
            // Ограничение распространяется только
            // на CREATED и UNDER_REVIEW.
            e.HasIndex(x => new
                {
                    x.StudentId,
                    x.ProjectId
                })
                .IsUnique()
                .HasFilter(
                    "status IN ('CREATED', 'UNDER_REVIEW')")
                .HasDatabaseName(
                    "uq_active_application_per_project");
        });

        // -------------------------------------------------
        // ProjectMembership -> project_memberships
        // -------------------------------------------------
        b.Entity<ProjectMembership>(e =>
        {
            e.ToTable("project_memberships");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id");

            e.Property(x => x.ProjectId)
                .HasColumnName("project_id");

            e.Property(x => x.StudentId)
                .HasColumnName("student_id");

            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(20);

            e.Property(x => x.JoinedAt)
                .HasColumnName("joined_at");

            e.Property(x => x.LeftAt)
                .HasColumnName("left_at");

            e.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Критичное ограничение базы данных:
            // один студент не может одновременно
            // состоять в двух ACTIVE проектах.
            //
            // Исторические LEFT и EXCLUDED
            // записи при этом сохраняются.
            e.HasIndex(x => x.StudentId)
                .IsUnique()
                .HasFilter("status = 'ACTIVE'")
                .HasDatabaseName(
                    "uq_one_active_project_per_student");
        });
    }
}