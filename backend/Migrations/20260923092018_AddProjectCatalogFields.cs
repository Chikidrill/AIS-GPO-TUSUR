using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AisGpo.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectCatalogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "projects",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "competencies",
                table: "projects",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "direction",
                table: "projects",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "faculty",
                table: "projects",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "goal",
                table: "projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "semester",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_places",
                table: "projects",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_code",
                table: "projects",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_projects_code",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "code",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "competencies",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "direction",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "faculty",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "goal",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "semester",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "total_places",
                table: "projects");
        }
    }
}
