using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vela.Migrations
{
    /// <inheritdoc />
    public partial class CleanupStorageRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bitcraft_auction_listing_state");

            migrationBuilder.DropTable(
                name: "bitcraft_claim_local_state");

            migrationBuilder.DropTable(
                name: "bitcraft_claim_state");

            migrationBuilder.DropTable(
                name: "bitcraft_claim_tech_state");

            migrationBuilder.DropTable(
                name: "bitcraft_closed_listing_state");

            migrationBuilder.DropTable(
                name: "bitcraft_empire_state");

            migrationBuilder.DropTable(
                name: "bitcraft_user_state");

            migrationBuilder.DropTable(
                name: "bitcraft_username_state");

            migrationBuilder.DropColumn(
                name: "module",
                table: "bitcraft_recipe");

            migrationBuilder.DropColumn(
                name: "module",
                table: "bitcraft_paving_tile_desc");

            migrationBuilder.DropColumn(
                name: "module",
                table: "bitcraft_item_list");

            migrationBuilder.DropColumn(
                name: "module",
                table: "bitcraft_item");

            migrationBuilder.DropColumn(
                name: "module",
                table: "bitcraft_claim_tech_desc");

            migrationBuilder.DropColumn(
                name: "module",
                table: "bitcraft_cargo_item");

            migrationBuilder.DropColumn(
                name: "module",
                table: "bitcraft_building_desc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "bitcraft_recipe",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "bitcraft_paving_tile_desc",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "bitcraft_item_list",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "bitcraft_item",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "bitcraft_claim_tech_desc",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "bitcraft_cargo_item",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "module",
                table: "bitcraft_building_desc",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "bitcraft_auction_listing_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    claim_id = table.Column<string>(type: "text", nullable: false),
                    is_cargo_item = table.Column<bool>(type: "boolean", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    stored_coins = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_auction_listing_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_claim_local_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    building_description_id = table.Column<int>(type: "integer", nullable: false),
                    location = table.Column<string>(type: "jsonb", nullable: true),
                    module = table.Column<string>(type: "text", nullable: false),
                    supplies = table.Column<int>(type: "integer", nullable: false),
                    treasury = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_claim_local_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_claim_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    is_neutral = table.Column<bool>(type: "boolean", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    owner_building_id = table.Column<string>(type: "text", nullable: false),
                    owner_player_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_claim_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_claim_tech_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    learned = table.Column<string>(type: "jsonb", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    researching = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_claim_tech_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_closed_listing_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    claim_id = table.Column<string>(type: "text", nullable: false),
                    item_stack = table.Column<string>(type: "jsonb", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<string>(type: "text", nullable: false),
                    timestamp = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_closed_listing_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_empire_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    shard_treasury = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_empire_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_user_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    can_sign_in = table.Column<bool>(type: "boolean", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    user_entity_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_user_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_username_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    username = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_username_state", x => x.id);
                });
        }
    }
}
