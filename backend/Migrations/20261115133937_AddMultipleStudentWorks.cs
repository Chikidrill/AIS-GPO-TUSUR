using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AisGpo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleStudentWorks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Старое ограничение разрешало только одну работу
            // студента в рамках проекта.
            migrationBuilder.DropIndex(
                name: "uq_student_work_project_student",
                table: "student_works");

            // Сначала добавляем nullable-колонку,
            // чтобы миграция могла примениться к существующим данным.
            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "student_works",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            // Для уже существующих работ создаём
            // техническое, но непустое название.
            migrationBuilder.Sql(
                """
                UPDATE student_works
                SET title = 'Работа #' || id
                WHERE title IS NULL;
                """);

            // После заполнения старых записей
            // название становится обязательным.
            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "student_works",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            // Индекс сохраняем для быстрых выборок,
            // но он больше не уникальный:
            // студент может иметь несколько работ
            // в одном проекте.
            migrationBuilder.CreateIndex(
                name: "ix_student_work_project_student",
                table: "student_works",
                columns: new[] { "project_id", "student_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_student_work_project_student",
                table: "student_works");

            migrationBuilder.DropColumn(
                name: "title",
                table: "student_works");

            migrationBuilder.CreateIndex(
                name: "uq_student_work_project_student",
                table: "student_works",
                columns: new[] { "project_id", "student_id" },
                unique: true);
        }
    }
}