using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vela.Migrations
{
    /// <inheritdoc />
    public partial class AddItemIconAssetName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "icon_asset_name",
                table: "bitcraft_item",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "icon_asset_name",
                table: "bitcraft_cargo_item",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "icon_asset_name",
                table: "bitcraft_item");

            migrationBuilder.DropColumn(
                name: "icon_asset_name",
                table: "bitcraft_cargo_item");
        }
    }
}
