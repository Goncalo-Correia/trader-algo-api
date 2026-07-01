using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncJobErrors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_job_errors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SyncJobId = table.Column<long>(type: "bigint", nullable: false),
                    Symbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Interval = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CandleOpenTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_job_errors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sync_job_errors_sync_jobs_SyncJobId",
                        column: x => x.SyncJobId,
                        principalTable: "sync_jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_job_errors_Symbol_Interval_CandleOpenTime",
                table: "sync_job_errors",
                columns: new[] { "Symbol", "Interval", "CandleOpenTime" });

            migrationBuilder.CreateIndex(
                name: "IX_sync_job_errors_SyncJobId",
                table: "sync_job_errors",
                column: "SyncJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_job_errors");
        }
    }
}
