using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupAccessSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupBlackList",
                table: "AccessSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GroupWhiteList",
                table: "AccessSettings",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsGroupAccessEnabled",
                table: "AccessSettings",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupBlackList",
                table: "AccessSettings");

            migrationBuilder.DropColumn(
                name: "GroupWhiteList",
                table: "AccessSettings");

            migrationBuilder.DropColumn(
                name: "IsGroupAccessEnabled",
                table: "AccessSettings");
        }
    }
}
