using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vela.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeGraphDescriptors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "blocking_knowledges",
                table: "bitcraft_recipe",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "required_claim_tech_id",
                table: "bitcraft_recipe",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "required_knowledges",
                table: "bitcraft_recipe",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<float>(
                name: "stamina_requirement",
                table: "bitcraft_recipe",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "time_requirement",
                table: "bitcraft_recipe",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<string>(
                name: "tag",
                table: "bitcraft_item",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "functions",
                table: "bitcraft_building_desc",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "bitcraft_building_type_desc",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_building_type_desc", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_extraction_recipe",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    verb_phrase = table.Column<string>(type: "text", nullable: false),
                    resource_id = table.Column<int>(type: "integer", nullable: false),
                    cargo_id = table.Column<int>(type: "integer", nullable: false),
                    level_requirements = table.Column<string>(type: "jsonb", nullable: false),
                    tool_requirements = table.Column<string>(type: "jsonb", nullable: false),
                    consumed_item_stacks = table.Column<string>(type: "jsonb", nullable: false),
                    extracted_item_stacks = table.Column<string>(type: "jsonb", nullable: false),
                    time_requirement = table.Column<float>(type: "real", nullable: false),
                    stamina_requirement = table.Column<float>(type: "real", nullable: false),
                    range = table.Column<int>(type: "integer", nullable: false),
                    allow_use_hands = table.Column<bool>(type: "boolean", nullable: false),
                    required_knowledges = table.Column<string>(type: "jsonb", nullable: false),
                    blocking_knowledges = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_extraction_recipe", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_resource_desc",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    tag = table.Column<string>(type: "text", nullable: false),
                    rarity = table.Column<string>(type: "text", nullable: false),
                    max_health = table.Column<int>(type: "integer", nullable: false),
                    on_destroy_yield = table.Column<string>(type: "jsonb", nullable: false),
                    on_destroy_yield_resource_id = table.Column<int>(type: "integer", nullable: false),
                    has_compendium_entry = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_resource_desc", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_secondary_knowledge_desc",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_secondary_knowledge_desc", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_skill_desc",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    skill_type = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    skill_category = table.Column<string>(type: "text", nullable: false),
                    max_level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_skill_desc", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_tool_desc",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    item_id = table.Column<int>(type: "integer", nullable: false),
                    tool_type = table.Column<int>(type: "integer", nullable: false),
                    level = table.Column<int>(type: "integer", nullable: false),
                    power = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_tool_desc", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bitcraft_tool_type_desc",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    skill_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_tool_type_desc", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bitcraft_building_type_desc");

            migrationBuilder.DropTable(
                name: "bitcraft_extraction_recipe");

            migrationBuilder.DropTable(
                name: "bitcraft_resource_desc");

            migrationBuilder.DropTable(
                name: "bitcraft_secondary_knowledge_desc");

            migrationBuilder.DropTable(
                name: "bitcraft_skill_desc");

            migrationBuilder.DropTable(
                name: "bitcraft_tool_desc");

            migrationBuilder.DropTable(
                name: "bitcraft_tool_type_desc");

            migrationBuilder.DropColumn(
                name: "blocking_knowledges",
                table: "bitcraft_recipe");

            migrationBuilder.DropColumn(
                name: "required_claim_tech_id",
                table: "bitcraft_recipe");

            migrationBuilder.DropColumn(
                name: "required_knowledges",
                table: "bitcraft_recipe");

            migrationBuilder.DropColumn(
                name: "stamina_requirement",
                table: "bitcraft_recipe");

            migrationBuilder.DropColumn(
                name: "time_requirement",
                table: "bitcraft_recipe");

            migrationBuilder.DropColumn(
                name: "tag",
                table: "bitcraft_item");

            migrationBuilder.DropColumn(
                name: "functions",
                table: "bitcraft_building_desc");
        }
    }
}
