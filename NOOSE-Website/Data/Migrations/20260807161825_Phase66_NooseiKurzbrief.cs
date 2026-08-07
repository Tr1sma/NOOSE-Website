using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase66_NooseiKurzbrief : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Derived cache: wipe it. The column below is renamed, not recreated, so surviving rows would keep a
            // valid content hash next to rendered HTML where JSON is now expected — every panel would then show
            // "brief exists" with nothing in it.
            migrationBuilder.Sql("DELETE FROM KiZusammenfassungen;");

            migrationBuilder.DropColumn(
                name: "KurzfassungHtml",
                table: "KiZusammenfassungen");

            migrationBuilder.RenameColumn(
                name: "ZusammenfassungHtml",
                table: "KiZusammenfassungen",
                newName: "KurzbriefJson");

            migrationBuilder.AddColumn<int>(
                name: "PromptVersion",
                table: "KiZusammenfassungen",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "KiZusammenfassungen",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromptVersion",
                table: "KiZusammenfassungen");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "KiZusammenfassungen");

            migrationBuilder.RenameColumn(
                name: "KurzbriefJson",
                table: "KiZusammenfassungen",
                newName: "ZusammenfassungHtml");

            migrationBuilder.AddColumn<string>(
                name: "KurzfassungHtml",
                table: "KiZusammenfassungen",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }
    }
}
