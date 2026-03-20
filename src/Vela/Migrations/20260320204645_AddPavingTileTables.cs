using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vela.Migrations
{
    /// <inheritdoc />
    public partial class AddPavingTileTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bitcraft_paving_tile_desc",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    paving_duration = table.Column<float>(type: "real", nullable: false),
                    prefab_address = table.Column<string>(type: "text", nullable: false),
                    icon_address = table.Column<string>(type: "text", nullable: false),
                    input_cargo_id = table.Column<int>(type: "integer", nullable: false),
                    input_cargo_discovery_score = table.Column<int>(type: "integer", nullable: false),
                    full_discovery_score = table.Column<int>(type: "integer", nullable: false),
                    required_knowledges = table.Column<string>(type: "jsonb", nullable: false),
                    discovery_triggers = table.Column<string>(type: "jsonb", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_paving_tile_desc", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bitcraft_paving_tile_desc");
        }
    }
}
