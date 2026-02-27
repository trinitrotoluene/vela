using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vela.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUserModerationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bitcraft_user_moderation_state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bitcraft_user_moderation_state",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_entity_id = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    module = table.Column<string>(type: "text", nullable: false),
                    target_entity_id = table.Column<string>(type: "text", nullable: false),
                    user_moderation_policy = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitcraft_user_moderation_state", x => x.id);
                });
        }
    }
}
