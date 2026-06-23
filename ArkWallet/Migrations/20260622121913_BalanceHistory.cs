using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class BalanceHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BalanceSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TotalBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                    MainBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                    LongOrderReserveBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                    ShortOrderReserveBalance = table.Column<decimal>(type: "TEXT", nullable: false),
                    BalanceInTokens = table.Column<decimal>(type: "TEXT", nullable: false),
                    SnapshotDateTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TraderId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BalanceSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BalanceSnapshots_Traders_TraderId",
                        column: x => x.TraderId,
                        principalTable: "Traders",
                        principalColumn: "TelegramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceSnapshots_TraderId",
                table: "BalanceSnapshots",
                column: "TraderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BalanceSnapshots");
        }
    }
}
