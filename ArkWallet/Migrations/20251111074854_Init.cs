using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterTokens",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Rarity = table.Column<int>(type: "INTEGER", nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    TotalSupply = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterTokens", x => x.Symbol);
                });

            migrationBuilder.CreateTable(
                name: "Traders",
                columns: table => new
                {
                    TelegramId = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    Balance = table.Column<decimal>(type: "TEXT", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traders", x => x.TelegramId);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    TraderTelegramId = table.Column<long>(type: "INTEGER", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    AverageBuyPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortfolioItems_CharacterTokens_CharacterTokenId",
                        column: x => x.CharacterTokenId,
                        principalTable: "CharacterTokens",
                        principalColumn: "Symbol",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PortfolioItems_Traders_TraderTelegramId",
                        column: x => x.TraderTelegramId,
                        principalTable: "Traders",
                        principalColumn: "TelegramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeOrders",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "TEXT", nullable: false),
                    TraderTelegramId = table.Column<long>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    FilledQuantity = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeOrders_CharacterTokens_CharacterTokenId",
                        column: x => x.CharacterTokenId,
                        principalTable: "CharacterTokens",
                        principalColumn: "Symbol",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeOrders_Traders_TraderTelegramId",
                        column: x => x.TraderTelegramId,
                        principalTable: "Traders",
                        principalColumn: "TelegramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    BuyerId = table.Column<long>(type: "INTEGER", nullable: false),
                    SellerId = table.Column<long>(type: "INTEGER", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "TEXT", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Trades_CharacterTokens_CharacterTokenId",
                        column: x => x.CharacterTokenId,
                        principalTable: "CharacterTokens",
                        principalColumn: "Symbol",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Trades_Traders_BuyerId",
                        column: x => x.BuyerId,
                        principalTable: "Traders",
                        principalColumn: "TelegramId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Trades_Traders_SellerId",
                        column: x => x.SellerId,
                        principalTable: "Traders",
                        principalColumn: "TelegramId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_CharacterTokenId",
                table: "PortfolioItems",
                column: "CharacterTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_TraderTelegramId",
                table: "PortfolioItems",
                column: "TraderTelegramId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeOrders_CharacterTokenId",
                table: "TradeOrders",
                column: "CharacterTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeOrders_TraderTelegramId",
                table: "TradeOrders",
                column: "TraderTelegramId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_BuyerId",
                table: "Trades",
                column: "BuyerId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_CharacterTokenId",
                table: "Trades",
                column: "CharacterTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_Trades_SellerId",
                table: "Trades",
                column: "SellerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortfolioItems");

            migrationBuilder.DropTable(
                name: "TradeOrders");

            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "CharacterTokens");

            migrationBuilder.DropTable(
                name: "Traders");
        }
    }
}
