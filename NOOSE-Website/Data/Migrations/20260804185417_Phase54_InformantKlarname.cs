using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase54_InformantKlarname : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Klarname",
                table: "Informanten",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PersonId",
                table: "Informanten",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InformantKontakte",
                columns: table => new
                {
                    InformantId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kontakt = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notizen = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InformantKontakte", x => x.InformantId);
                    table.ForeignKey(
                        name: "FK_InformantKontakte_Informanten_InformantId",
                        column: x => x.InformantId,
                        principalTable: "Informanten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // carry contact data into the renamed table before the old one goes away
            migrationBuilder.Sql(
                "INSERT INTO `InformantKontakte` (`InformantId`, `Kontakt`, `Notizen`) " +
                "SELECT `InformantId`, `Kontakt`, `Notizen` FROM `InformantIdentitaeten`;");

            // the real name becomes the informant's public name; fall back to the dropped codename
            migrationBuilder.Sql(
                "UPDATE `Informanten` i LEFT JOIN `InformantIdentitaeten` x ON x.`InformantId` = i.`Id` " +
                "SET i.`Klarname` = COALESCE(NULLIF(x.`Klarname`, ''), NULLIF(i.`Deckname`, ''));");

            migrationBuilder.DropTable(
                name: "InformantIdentitaeten");

            migrationBuilder.DropColumn(
                name: "Deckname",
                table: "Informanten");

            migrationBuilder.CreateIndex(
                name: "IX_Informanten_PersonId",
                table: "Informanten",
                column: "PersonId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Informanten_Personen_PersonId",
                table: "Informanten",
                column: "PersonId",
                principalTable: "Personen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Informanten_Personen_PersonId",
                table: "Informanten");

            migrationBuilder.DropIndex(
                name: "IX_Informanten_PersonId",
                table: "Informanten");

            migrationBuilder.AddColumn<string>(
                name: "Deckname",
                table: "Informanten",
                type: "varchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InformantIdentitaeten",
                columns: table => new
                {
                    InformantId = table.Column<string>(type: "varchar(255)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Kontakt = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notizen = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Klarname = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InformantIdentitaeten", x => x.InformantId);
                    table.ForeignKey(
                        name: "FK_InformantIdentitaeten_Informanten_InformantId",
                        column: x => x.InformantId,
                        principalTable: "Informanten",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // push the name back into the identity table and reuse it as the codename
            migrationBuilder.Sql(
                "INSERT INTO `InformantIdentitaeten` (`InformantId`, `Klarname`, `Kontakt`, `Notizen`) " +
                "SELECT i.`Id`, COALESCE(i.`Klarname`, ''), k.`Kontakt`, k.`Notizen` " +
                "FROM `Informanten` i LEFT JOIN `InformantKontakte` k ON k.`InformantId` = i.`Id`;");

            migrationBuilder.Sql("UPDATE `Informanten` SET `Deckname` = COALESCE(`Klarname`, '');");

            migrationBuilder.DropTable(
                name: "InformantKontakte");

            migrationBuilder.DropColumn(
                name: "Klarname",
                table: "Informanten");

            migrationBuilder.DropColumn(
                name: "PersonId",
                table: "Informanten");
        }
    }
}
