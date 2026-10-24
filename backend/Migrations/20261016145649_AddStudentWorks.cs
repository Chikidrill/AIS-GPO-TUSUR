using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AisGpo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentWorks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_works",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    project_id = table.Column<long>(type: "bigint", nullable: false),
                    student_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_works", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_works_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_works_users_student_id",
                        column: x => x.student_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_revisions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    student_work_id = table.Column<long>(type: "bigint", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    text = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    grade = table.Column<int>(type: "integer", nullable: true),
                    review_comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    reviewed_by = table.Column<long>(type: "bigint", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_revisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_revisions_student_works_student_work_id",
                        column: x => x.student_work_id,
                        principalTable: "student_works",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_work_revisions_users_reviewed_by",
                        column: x => x.reviewed_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_files",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    work_revision_id = table.Column<long>(type: "bigint", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_files", x => x.id);
                    table.ForeignKey(
                        name: "FK_work_files_work_revisions_work_revision_id",
                        column: x => x.work_revision_id,
                        principalTable: "work_revisions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_works_student_id",
                table: "student_works",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "uq_student_work_project_student",
                table: "student_works",
                columns: new[] { "project_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_files_storage_key",
                table: "work_files",
                column: "storage_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_files_work_revision_id",
                table: "work_files",
                column: "work_revision_id");

            migrationBuilder.CreateIndex(
                name: "IX_work_revisions_reviewed_by",
                table: "work_revisions",
                column: "reviewed_by");

            migrationBuilder.CreateIndex(
                name: "uq_work_revision_number",
                table: "work_revisions",
                columns: new[] { "student_work_id", "version_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_files");

            migrationBuilder.DropTable(
                name: "work_revisions");

            migrationBuilder.DropTable(
                name: "student_works");
        }
    }
}
