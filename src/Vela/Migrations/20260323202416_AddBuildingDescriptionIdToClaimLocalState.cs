using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vela.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingDescriptionIdToClaimLocalState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "building_description_id",
                table: "bitcraft_claim_local_state",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "building_description_id",
                table: "bitcraft_claim_local_state");
        }
    }
}
