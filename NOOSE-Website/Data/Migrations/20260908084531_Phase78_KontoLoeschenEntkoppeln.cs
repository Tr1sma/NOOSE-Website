using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase78_KontoLoeschenEntkoppeln : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "HandlerId",
                table: "Informanten",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "AgentId",
                table: "Finanzierungsbudgetperioden",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "AgentId",
                table: "Finanzierungsantraege",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BenutzerId",
                table: "BuergerProfile",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "BearbeiterId",
                table: "AsservatEintraege",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<string>(
                name: "OpferAgentId",
                table: "AgentEntfuehrungen",
                type: "varchar(255)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Only runnable while no account has been deleted yet. Each of these six columns carries a Restrict
        /// foreign key to AspNetUsers, so writing the empty string back into a detached row fails on the key
        /// (no user has id ""), and two detached rows would additionally collide in the unique index over
        /// BuergerProfile.BenutzerId and over Finanzierungsbudgetperioden(AgentId, Jahr, Monat). That is a
        /// property of the change, not an oversight: once an account is gone, the pointer it left behind cannot
        /// be restored, and this migration deliberately does not invent a replacement for it.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Informanten",
                keyColumn: "HandlerId",
                keyValue: null,
                column: "HandlerId",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "HandlerId",
                table: "Informanten",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Finanzierungsbudgetperioden",
                keyColumn: "AgentId",
                keyValue: null,
                column: "AgentId",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "AgentId",
                table: "Finanzierungsbudgetperioden",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Finanzierungsantraege",
                keyColumn: "AgentId",
                keyValue: null,
                column: "AgentId",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "AgentId",
                table: "Finanzierungsantraege",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "BuergerProfile",
                keyColumn: "BenutzerId",
                keyValue: null,
                column: "BenutzerId",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "BenutzerId",
                table: "BuergerProfile",
                type: "varchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(64)",
                oldMaxLength: 64,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "AsservatEintraege",
                keyColumn: "BearbeiterId",
                keyValue: null,
                column: "BearbeiterId",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "BearbeiterId",
                table: "AsservatEintraege",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "AgentEntfuehrungen",
                keyColumn: "OpferAgentId",
                keyValue: null,
                column: "OpferAgentId",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "OpferAgentId",
                table: "AgentEntfuehrungen",
                type: "varchar(255)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
