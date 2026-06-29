using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePotrfolioLogic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AverageReservePrice",
                table: "PortfolioItems",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageSellPrice",
                table: "PortfolioItems",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ReserveQuantity",
                table: "PortfolioItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SellingQuantity",
                table: "PortfolioItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageReservePrice",
                table: "PortfolioItems");

            migrationBuilder.DropColumn(
                name: "AverageSellPrice",
                table: "PortfolioItems");

            migrationBuilder.DropColumn(
                name: "ReserveQuantity",
                table: "PortfolioItems");

            migrationBuilder.DropColumn(
                name: "SellingQuantity",
                table: "PortfolioItems");
        }
    }
}
