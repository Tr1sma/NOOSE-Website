using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NOOSE_Website.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase34_MehrstufigeVerschlusssachen : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheHRB",
                table: "Vorgaenge",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheTRU",
                table: "Vorgaenge",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VeralterungDeaktiviert",
                table: "Vorgaenge",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VeralterungDeaktiviert",
                table: "Taskforces",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheHRB",
                table: "Personengruppen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheTRU",
                table: "Personengruppen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VeralterungDeaktiviert",
                table: "Personengruppen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheHRB",
                table: "Personen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheTRU",
                table: "Personen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VeralterungDeaktiviert",
                table: "Personen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheHRB",
                table: "Parteien",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheTRU",
                table: "Parteien",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VeralterungDeaktiviert",
                table: "Parteien",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheHRB",
                table: "Operationen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheTRU",
                table: "Operationen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VeralterungDeaktiviert",
                table: "Operationen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheHRB",
                table: "Fraktionen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IstVerschlusssacheTRU",
                table: "Fraktionen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VeralterungDeaktiviert",
                table: "Fraktionen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "VeralterungDeaktiviert",
                table: "AktualitaetsSchwellen",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheHRB",
                table: "Vorgaenge");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheTRU",
                table: "Vorgaenge");

            migrationBuilder.DropColumn(
                name: "VeralterungDeaktiviert",
                table: "Vorgaenge");

            migrationBuilder.DropColumn(
                name: "VeralterungDeaktiviert",
                table: "Taskforces");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheHRB",
                table: "Personengruppen");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheTRU",
                table: "Personengruppen");

            migrationBuilder.DropColumn(
                name: "VeralterungDeaktiviert",
                table: "Personengruppen");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheHRB",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheTRU",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "VeralterungDeaktiviert",
                table: "Personen");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheHRB",
                table: "Parteien");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheTRU",
                table: "Parteien");

            migrationBuilder.DropColumn(
                name: "VeralterungDeaktiviert",
                table: "Parteien");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheHRB",
                table: "Operationen");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheTRU",
                table: "Operationen");

            migrationBuilder.DropColumn(
                name: "VeralterungDeaktiviert",
                table: "Operationen");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheHRB",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "IstVerschlusssacheTRU",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "VeralterungDeaktiviert",
                table: "Fraktionen");

            migrationBuilder.DropColumn(
                name: "VeralterungDeaktiviert",
                table: "AktualitaetsSchwellen");
        }
    }
}
