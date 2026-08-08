using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase55_InformantFraktionUndKontakt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FraktionId",
                table: "Informanten",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Kontakt",
                table: "Informanten",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Notizen",
                table: "Informanten",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // contact data loses its separate tier and moves onto the record itself
            migrationBuilder.Sql(
                "UPDATE `Informanten` i JOIN `InformantKontakte` k ON k.`InformantId` = i.`Id` " +
                "SET i.`Kontakt` = k.`Kontakt`, i.`Notizen` = k.`Notizen`;");

            migrationBuilder.DropTable(
                name: "InformantKontakte");

            migrationBuilder.CreateIndex(
                name: "IX_Informanten_FraktionId",
                table: "Informanten",
                column: "FraktionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Informanten_Fraktionen_FraktionId",
                table: "Informanten",
                column: "FraktionId",
                principalTable: "Fraktionen",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Informanten_Fraktionen_FraktionId",
                table: "Informanten");

            migrationBuilder.DropIndex(
                name: "IX_Informanten_FraktionId",
                table: "Informanten");

            migrationBuilder.DropColumn(
                name: "FraktionId",
                table: "Informanten");

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

            // push the contact data back into its own table before the columns go away
            migrationBuilder.Sql(
                "INSERT INTO `InformantKontakte` (`InformantId`, `Kontakt`, `Notizen`) " +
                "SELECT `Id`, `Kontakt`, `Notizen` FROM `Informanten` " +
                "WHERE `Kontakt` IS NOT NULL OR `Notizen` IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "Kontakt",
                table: "Informanten");

            migrationBuilder.DropColumn(
                name: "Notizen",
                table: "Informanten");
        }
    }
}
