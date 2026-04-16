using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderAlgoApi.Migrations
{
    /// <inheritdoc />
    public partial class AddFiveMinuteInterval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "intervals",
                columns: new[] { "Id", "Code", "CreatedAt", "DisplayName", "Duration", "IsActive" },
                values: new object[] { 2, "5m", new DateTimeOffset(new DateTime(2026, 4, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "5 Minute", new TimeSpan(0, 0, 5, 0, 0), true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "intervals",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
