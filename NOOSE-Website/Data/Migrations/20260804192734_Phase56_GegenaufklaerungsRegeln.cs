using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase56_GegenaufklaerungsRegeln : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GegenaufklaerungsRegeln",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Beschreibung = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Schweregrad = table.Column<int>(type: "int", nullable: false),
                    IstAktiv = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Reihenfolge = table.Column<int>(type: "int", nullable: false),
                    DefinitionJson = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ErstelltAm = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ErstelltVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GeaendertAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeaendertVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IstGeloescht = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    GeloeschtAm = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    GeloeschtVonId = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GegenaufklaerungsRegeln", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_GegenaufklaerungsRegeln_IstAktiv_Reihenfolge",
                table: "GegenaufklaerungsRegeln",
                columns: new[] { "IstAktiv", "Reihenfolge" });

            // the three patterns the cockpit shipped with, now editable; ids match CounterIntelRuleDefaults
            migrationBuilder.InsertData(
                table: "GegenaufklaerungsRegeln",
                columns: new[] { "Id", "Name", "Beschreibung", "Schweregrad", "IstAktiv", "Reihenfolge", "DefinitionJson", "ErstelltAm", "IstGeloescht" },
                values: new object[,]
                {
                    {
                        "11111111-c0de-4a01-9000-000000000001",
                        "Off-Hours",
                        "Zugriffe außerhalb der Dienstzeit (22–6 Uhr) über den gesamten Zeitraum.",
                        1, true, 10,
                        "{\"WindowDays\":30,\"Actions\":[0],\"EntityTypes\":[],\"EntityIds\":[],\"Classifications\":[],\"TagIds\":[],\"ActorRanks\":[],\"ActorIds\":[],\"ExcludedActorIds\":[],\"PartnerScope\":0,\"FromHour\":22,\"ToHour\":6,\"Weekdays\":[],\"CountMode\":0,\"Bucket\":0,\"SlidingMinutes\":60,\"Threshold\":15}",
                        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), false
                    },
                    {
                        "11111111-c0de-4a01-9000-000000000002",
                        "Massen-Zugriff",
                        "Auffällig viele verschiedene Akten an einem einzigen Tag geöffnet.",
                        2, true, 20,
                        "{\"WindowDays\":30,\"Actions\":[0],\"EntityTypes\":[],\"EntityIds\":[],\"Classifications\":[],\"TagIds\":[],\"ActorRanks\":[],\"ActorIds\":[],\"ExcludedActorIds\":[],\"PartnerScope\":0,\"FromHour\":0,\"ToHour\":0,\"Weekdays\":[],\"CountMode\":1,\"Bucket\":1,\"SlidingMinutes\":60,\"Threshold\":40}",
                        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), false
                    },
                    {
                        "11111111-c0de-4a01-9000-000000000003",
                        "Zugriffs-Burst",
                        "Sehr viele Zugriffe innerhalb einer einzigen Stunde.",
                        1, true, 30,
                        "{\"WindowDays\":30,\"Actions\":[0],\"EntityTypes\":[],\"EntityIds\":[],\"Classifications\":[],\"TagIds\":[],\"ActorRanks\":[],\"ActorIds\":[],\"ExcludedActorIds\":[],\"PartnerScope\":0,\"FromHour\":0,\"ToHour\":0,\"Weekdays\":[],\"CountMode\":0,\"Bucket\":2,\"SlidingMinutes\":60,\"Threshold\":30}",
                        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), false
                    },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GegenaufklaerungsRegeln");
        }
    }
}
