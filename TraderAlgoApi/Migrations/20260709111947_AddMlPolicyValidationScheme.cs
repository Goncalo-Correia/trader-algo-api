using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMlPolicyValidationScheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill existing rows (and default new inserts that omit the column) to the sidecar's
            // "single" default so the persisted value is always a valid scheme.
            migrationBuilder.AddColumn<string>(
                name: "ValidationScheme",
                table: "ml_policies",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "single");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ValidationScheme",
                table: "ml_policies");
        }
    }
}
