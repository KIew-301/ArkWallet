using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class AddActivePowerToBots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PriceCandles_CharacterTokenId",
                table: "PriceCandles");

            migrationBuilder.AddColumn<decimal>(
                name: "ActivePower",
                table: "MarketMakerBots",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                @"UPDATE ""MarketMakerBots"" SET ""ActivePower"" = ""BasePower"";");

            migrationBuilder.CreateIndex(
                name: "IX_PriceCandles_CharacterTokenId_Timestamp",
                table: "PriceCandles",
                columns: new[] { "CharacterTokenId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PriceCandles_CharacterTokenId_Timestamp",
                table: "PriceCandles");

            migrationBuilder.DropColumn(
                name: "ActivePower",
                table: "MarketMakerBots");

            migrationBuilder.CreateIndex(
                name: "IX_PriceCandles_CharacterTokenId",
                table: "PriceCandles",
                column: "CharacterTokenId");
        }
    }
}
