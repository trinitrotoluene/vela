using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vela.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimTechTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bitcraft_claim_tech_desc",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    tech_type = table.Column<string>(type: "text", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_claim_tech_desc", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_claim_tech_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    learned = table.Column<string>(type: "jsonb", nullable: false),
                    researching = table.Column<int>(type: "integer", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_claim_tech_state", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bitcraft_claim_tech_desc");

            migrationBuilder.DropTable(
                name: "bitcraft_claim_tech_state");
        }
    }
}
