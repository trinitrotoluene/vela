using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vela.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bitcraft_auction_listing_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<string>(type: "text", nullable: false),
                    claim_id = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    stored_coins = table.Column<int>(type: "integer", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    is_cargo_item = table.Column<bool>(type: "boolean", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_auction_listing_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_building_desc",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_building_desc", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_cargo_item",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    volume = table.Column<int>(type: "integer", nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    rarity = table.Column<string>(type: "text", nullable: false),
                    tag = table.Column<string>(type: "text", nullable: false),
                    not_pickupable = table.Column<bool>(type: "boolean", nullable: false),
                    blocks_path = table.Column<bool>(type: "boolean", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_cargo_item", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_claim_local_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    location = table.Column<string>(type: "jsonb", nullable: true),
                    supplies = table.Column<int>(type: "integer", nullable: false),
                    treasury = table.Column<long>(type: "bigint", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
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
                    owner_player_id = table.Column<string>(type: "text", nullable: false),
                    owner_building_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_neutral = table.Column<bool>(type: "boolean", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_claim_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_empire_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    shard_treasury = table.Column<int>(type: "integer", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_empire_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_item",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    volume = table.Column<int>(type: "integer", nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    rarity = table.Column<string>(type: "text", nullable: false),
                    item_list_id = table.Column<int>(type: "integer", nullable: false),
                    has_compendium_entry = table.Column<bool>(type: "boolean", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_item", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_item_list",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    possibilities = table.Column<string>(type: "jsonb", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_item_list", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_recipe",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name_format_string = table.Column<string>(type: "text", nullable: false),
                    building_requirement = table.Column<string>(type: "jsonb", nullable: true),
                    level_requirements = table.Column<string>(type: "jsonb", nullable: false),
                    tool_requirements = table.Column<string>(type: "jsonb", nullable: false),
                    consumed_item_stacks = table.Column<string>(type: "jsonb", nullable: false),
                    produced_item_stacks = table.Column<string>(type: "jsonb", nullable: false),
                    is_passive = table.Column<bool>(type: "boolean", nullable: false),
                    actions_required = table.Column<int>(type: "integer", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_recipe", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_user_moderation_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    created_by_entity_id = table.Column<string>(type: "text", nullable: false),
                    target_entity_id = table.Column<string>(type: "text", nullable: false),
                    user_moderation_policy = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_user_moderation_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_user_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    user_entity_id = table.Column<string>(type: "text", nullable: false),
                    can_sign_in = table.Column<bool>(type: "boolean", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
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
                    username = table.Column<string>(type: "text", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_username_state", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bitcraft_auction_listing_state");

            migrationBuilder.DropTable(
                name: "bitcraft_building_desc");

            migrationBuilder.DropTable(
                name: "bitcraft_cargo_item");

            migrationBuilder.DropTable(
                name: "bitcraft_claim_local_state");

            migrationBuilder.DropTable(
                name: "bitcraft_claim_state");

            migrationBuilder.DropTable(
                name: "bitcraft_empire_state");

            migrationBuilder.DropTable(
                name: "bitcraft_item");

            migrationBuilder.DropTable(
                name: "bitcraft_item_list");

            migrationBuilder.DropTable(
                name: "bitcraft_recipe");

            migrationBuilder.DropTable(
                name: "bitcraft_user_moderation_state");

            migrationBuilder.DropTable(
                name: "bitcraft_user_state");

            migrationBuilder.DropTable(
                name: "bitcraft_username_state");
        }
    }
}
