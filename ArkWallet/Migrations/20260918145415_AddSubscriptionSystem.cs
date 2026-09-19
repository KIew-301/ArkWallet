using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionExpiresAtUtc",
                table: "Traders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionId",
                table: "Traders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PowerDeviationCoeff",
                table: "MarketMakerBots",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    PriceRubles = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxOrders = table.Column<int>(type: "integer", nullable: false),
                    MaxMiningMachines = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPurchaseHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TraderId = table.Column<long>(type: "bigint", nullable: false),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    PriceRubles = table.Column<decimal>(type: "numeric", nullable: false),
                    PurchasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TransactionId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPurchaseHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPurchaseHistory_Subscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "Subscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SubscriptionPurchaseHistory_Traders_TraderId",
                        column: x => x.TraderId,
                        principalTable: "Traders",
                        principalColumn: "TelegramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Traders_SubscriptionId",
                table: "Traders",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchaseHistory_SubscriptionId",
                table: "SubscriptionPurchaseHistory",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPurchaseHistory_TraderId",
                table: "SubscriptionPurchaseHistory",
                column: "TraderId");

            migrationBuilder.AddForeignKey(
                name: "FK_Traders_Subscriptions_SubscriptionId",
                table: "Traders",
                column: "SubscriptionId",
                principalTable: "Subscriptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Traders_Subscriptions_SubscriptionId",
                table: "Traders");

            migrationBuilder.DropTable(
                name: "SubscriptionPurchaseHistory");

            migrationBuilder.DropTable(
                name: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Traders_SubscriptionId",
                table: "Traders");

            migrationBuilder.DropColumn(
                name: "SubscriptionExpiresAtUtc",
                table: "Traders");

            migrationBuilder.DropColumn(
                name: "SubscriptionId",
                table: "Traders");

            migrationBuilder.DropColumn(
                name: "PowerDeviationCoeff",
                table: "MarketMakerBots");
        }
    }
}
