using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Oeffentlich08_HinweisUebernahme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No schema change: the takeover writes existing tables. Only the fourth built-in counter-intelligence
            // rule is seeded, mirrored from CounterIntelRuleDefaults with a frozen timestamp, exactly as Phase56 did.
            migrationBuilder.InsertData(
                table: "GegenaufklaerungsRegeln",
                columns: new[] { "Id", "Name", "Beschreibung", "Schweregrad", "IstAktiv", "Reihenfolge", "DefinitionJson", "ErstelltAm", "IstGeloescht" },
                values: new object[,]
                {
                    {
                        "11111111-c0de-4a01-9000-000000000004",
                        "Hinweisgeber im eigenen Umfeld",
                        "Ein Bürgerhinweis über eine Person, mit der der Hinweisgeber eine Organisation teilt.",
                        2, true, 40,
                        "{\"WindowDays\":30,\"Actions\":[1],\"EntityTypes\":[\"Hinweis\"],\"EntityIds\":[],\"Classifications\":[],\"TagIds\":[],\"ActorRanks\":[],\"ActorIds\":[],\"ExcludedActorIds\":[],\"PartnerScope\":0,\"ActorSharesOrgWithTarget\":true,\"FromHour\":0,\"ToHour\":0,\"Weekdays\":[],\"CountMode\":0,\"Bucket\":0,\"SlidingMinutes\":60,\"Threshold\":1}",
                        new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), false
                    },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "GegenaufklaerungsRegeln",
                keyColumn: "Id",
                keyValue: "11111111-c0de-4a01-9000-000000000004");
        }
    }
}
