using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class RemovePowerDeviationCoeff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PowerDeviationCoeff",
                table: "MarketMakerBots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PowerDeviationCoeff",
                table: "MarketMakerBots",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
