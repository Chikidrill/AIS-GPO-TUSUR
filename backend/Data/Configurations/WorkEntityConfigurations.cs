using AisGpo.Api.Domain.Works;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AisGpo.Api.Data.Configurations;

/// <summary>
/// Конфигурация таблицы работ студентов.
/// </summary>
public sealed class StudentWorkConfiguration
    : IEntityTypeConfiguration<StudentWork>
{
    public void Configure(
        EntityTypeBuilder<StudentWork> e)
    {
        e.ToTable("student_works");

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
            .HasMaxLength(30)
            .IsRequired();

        e.Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        e.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        // У одного студента в рамках одного проекта
        // существует одна StudentWork.
        // Повторные отправки являются WorkRevision.
        e.HasIndex(x => new
        {
            x.ProjectId,
            x.StudentId
        })
            .IsUnique()
            .HasDatabaseName(
                "uq_student_work_project_student");

        e.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Конфигурация таблицы версий работ.
/// </summary>
public sealed class WorkRevisionConfiguration
    : IEntityTypeConfiguration<WorkRevision>
{
    public void Configure(
        EntityTypeBuilder<WorkRevision> e)
    {
        e.ToTable("work_revisions");

        e.HasKey(x => x.Id);

        e.Property(x => x.Id)
            .HasColumnName("id");

        e.Property(x => x.StudentWorkId)
            .HasColumnName("student_work_id");

        e.Property(x => x.VersionNumber)
            .HasColumnName("version_number");

        e.Property(x => x.Text)
            .HasColumnName("text");

        e.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        e.Property(x => x.Grade)
            .HasColumnName("grade");

        e.Property(x => x.ReviewComment)
            .HasColumnName("review_comment")
            .HasMaxLength(4000);

        e.Property(x => x.ReviewedById)
            .HasColumnName("reviewed_by");

        e.Property(x => x.SubmittedAt)
            .HasColumnName("submitted_at");

        e.Property(x => x.ReviewedAt)
            .HasColumnName("reviewed_at");

        // Номер версии уникален внутри одной работы.
        e.HasIndex(x => new
        {
            x.StudentWorkId,
            x.VersionNumber
        })
            .IsUnique()
            .HasDatabaseName(
                "uq_work_revision_number");

        e.HasOne(x => x.StudentWork)
            .WithMany(x => x.Revisions)
            .HasForeignKey(x => x.StudentWorkId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.ReviewedBy)
            .WithMany()
            .HasForeignKey(x => x.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Конфигурация таблицы файлов работ.
/// </summary>
public sealed class WorkFileConfiguration
    : IEntityTypeConfiguration<WorkFile>
{
    public void Configure(
        EntityTypeBuilder<WorkFile> e)
    {
        e.ToTable("work_files");

        e.HasKey(x => x.Id);

        e.Property(x => x.Id)
            .HasColumnName("id");

        e.Property(x => x.WorkRevisionId)
            .HasColumnName("work_revision_id");

        e.Property(x => x.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(255)
            .IsRequired();

        e.Property(x => x.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(500)
            .IsRequired();

        e.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(255)
            .IsRequired();

        e.Property(x => x.SizeBytes)
            .HasColumnName("size_bytes");

        e.Property(x => x.UploadedAt)
            .HasColumnName("uploaded_at");

        // Один физический объект хранилища
        // должен соответствовать только одной записи.
        e.HasIndex(x => x.StorageKey)
            .IsUnique();

        e.HasOne(x => x.WorkRevision)
            .WithMany(x => x.Files)
            .HasForeignKey(x => x.WorkRevisionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}