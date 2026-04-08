using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContosoDashboard.Migrations
{
    /// <inheritdoc />
    public partial class FixSeedDataStaticDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Announcements",
                keyColumn: "AnnouncementId",
                keyValue: 1,
                columns: new[] { "ExpiryDate", "PublishDate" },
                values: new object[] { new DateTime(2025, 1, 31, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "ProjectMembers",
                keyColumn: "ProjectMemberId",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2024, 12, 2, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "ProjectMembers",
                keyColumn: "ProjectMemberId",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2024, 12, 2, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "ProjectId",
                keyValue: 1,
                columns: new[] { "CreatedDate", "StartDate", "TargetCompletionDate", "UpdatedDate" },
                values: new object[] { new DateTime(2024, 12, 2, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12, 2, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 3, 2, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "TaskId",
                keyValue: 1,
                columns: new[] { "CreatedDate", "DueDate", "UpdatedDate" },
                values: new object[] { new DateTime(2024, 12, 2, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12, 12, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12, 12, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "TaskId",
                keyValue: 2,
                columns: new[] { "CreatedDate", "DueDate", "UpdatedDate" },
                values: new object[] { new DateTime(2024, 12, 7, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 6, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "TaskId",
                keyValue: 3,
                columns: new[] { "CreatedDate", "DueDate", "UpdatedDate" },
                values: new object[] { new DateTime(2024, 12, 12, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2025, 1, 11, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2024, 12, 12, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Announcements",
                keyColumn: "AnnouncementId",
                keyValue: 1,
                columns: new[] { "ExpiryDate", "PublishDate" },
                values: new object[] { new DateTime(2026, 5, 7, 17, 40, 21, 118, DateTimeKind.Utc).AddTicks(1889), new DateTime(2026, 4, 7, 17, 40, 21, 118, DateTimeKind.Utc).AddTicks(1719) });

            migrationBuilder.UpdateData(
                table: "ProjectMembers",
                keyColumn: "ProjectMemberId",
                keyValue: 1,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 8, 17, 40, 21, 118, DateTimeKind.Utc).AddTicks(226));

            migrationBuilder.UpdateData(
                table: "ProjectMembers",
                keyColumn: "ProjectMemberId",
                keyValue: 2,
                column: "AssignedDate",
                value: new DateTime(2026, 3, 8, 17, 40, 21, 118, DateTimeKind.Utc).AddTicks(393));

            migrationBuilder.UpdateData(
                table: "Projects",
                keyColumn: "ProjectId",
                keyValue: 1,
                columns: new[] { "CreatedDate", "StartDate", "TargetCompletionDate", "UpdatedDate" },
                values: new object[] { new DateTime(2026, 3, 8, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(5723), new DateTime(2026, 3, 8, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(4959), new DateTime(2026, 6, 6, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(5244), new DateTime(2026, 4, 7, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(5893) });

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "TaskId",
                keyValue: 1,
                columns: new[] { "CreatedDate", "DueDate", "UpdatedDate" },
                values: new object[] { new DateTime(2026, 3, 8, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(8576), new DateTime(2026, 3, 18, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(7862), new DateTime(2026, 3, 18, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(8735) });

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "TaskId",
                keyValue: 2,
                columns: new[] { "CreatedDate", "DueDate", "UpdatedDate" },
                values: new object[] { new DateTime(2026, 3, 13, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(8905), new DateTime(2026, 4, 12, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(8903), new DateTime(2026, 4, 7, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(8906) });

            migrationBuilder.UpdateData(
                table: "Tasks",
                keyColumn: "TaskId",
                keyValue: 3,
                columns: new[] { "CreatedDate", "DueDate", "UpdatedDate" },
                values: new object[] { new DateTime(2026, 3, 18, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(8910), new DateTime(2026, 4, 17, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(8909), new DateTime(2026, 3, 18, 17, 40, 21, 117, DateTimeKind.Utc).AddTicks(8911) });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 1,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 7, 17, 40, 21, 116, DateTimeKind.Utc).AddTicks(7131));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 2,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 7, 17, 40, 21, 116, DateTimeKind.Utc).AddTicks(7685));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 3,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 7, 17, 40, 21, 116, DateTimeKind.Utc).AddTicks(7688));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "UserId",
                keyValue: 4,
                column: "CreatedDate",
                value: new DateTime(2026, 4, 7, 17, 40, 21, 116, DateTimeKind.Utc).AddTicks(7691));
        }
    }
}
