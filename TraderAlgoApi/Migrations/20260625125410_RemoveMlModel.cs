using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMlModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ml_policies_ml_models_ModelId",
                table: "ml_policies");

            migrationBuilder.DropTable(
                name: "ml_models");

            migrationBuilder.DropIndex(
                name: "IX_ml_policies_ModelId",
                table: "ml_policies");

            migrationBuilder.DropColumn(
                name: "ModelId",
                table: "ml_policies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModelId",
                table: "ml_policies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ml_models",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ml_models", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ml_models",
                columns: new[] { "Id", "Name" },
                values: new object[] { 1, "ppo-v1" });

            migrationBuilder.CreateIndex(
                name: "IX_ml_policies_ModelId",
                table: "ml_policies",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_ml_models_Name",
                table: "ml_models",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ml_policies_ml_models_ModelId",
                table: "ml_policies",
                column: "ModelId",
                principalTable: "ml_models",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
