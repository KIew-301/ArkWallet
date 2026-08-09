using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class BalanceSnapshotTraderDateTimeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BalanceSnapshots_TraderId",
                table: "BalanceSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_BalanceSnapshots_TraderId_SnapshotDateTime",
                table: "BalanceSnapshots",
                columns: new[] { "TraderId", "SnapshotDateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BalanceSnapshots_TraderId_SnapshotDateTime",
                table: "BalanceSnapshots");

            migrationBuilder.CreateIndex(
                name: "IX_BalanceSnapshots_TraderId",
                table: "BalanceSnapshots",
                column: "TraderId");
        }
    }
}
