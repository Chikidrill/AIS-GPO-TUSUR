using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AisGpo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentGroupAndProjectDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "group_number",
                table: "student_profiles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "department",
                table: "projects",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "supervisor_id",
                table: "projects",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_supervisor_id",
                table: "projects",
                column: "supervisor_id");

            migrationBuilder.AddForeignKey(
                name: "FK_projects_users_supervisor_id",
                table: "projects",
                column: "supervisor_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_projects_users_supervisor_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_supervisor_id",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "group_number",
                table: "student_profiles");

            migrationBuilder.DropColumn(
                name: "department",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "supervisor_id",
                table: "projects");
        }
    }
}
