using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_job_statuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_job_statuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sync_job_types",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_job_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sync_jobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TypeId = table.Column<int>(type: "integer", nullable: false),
                    StatusId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalUnits = table.Column<int>(type: "integer", nullable: false),
                    CompletedUnits = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_jobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sync_jobs_sync_job_statuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "sync_job_statuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_sync_jobs_sync_job_types_TypeId",
                        column: x => x.TypeId,
                        principalTable: "sync_job_types",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "sync_job_statuses",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Pending" },
                    { 2, "Running" },
                    { 3, "Completed" },
                    { 4, "Failed" },
                    { 5, "Cancelled" }
                });

            migrationBuilder.InsertData(
                table: "sync_job_types",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "DataCollectorFullSync" },
                    { 2, "DataCollectorPartialSync" },
                    { 3, "IndicatorFullSync" },
                    { 4, "IndicatorPartialSync" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_jobs_StatusId",
                table: "sync_jobs",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_sync_jobs_TypeId",
                table: "sync_jobs",
                column: "TypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_jobs");

            migrationBuilder.DropTable(
                name: "sync_job_statuses");

            migrationBuilder.DropTable(
                name: "sync_job_types");
        }
    }
}
