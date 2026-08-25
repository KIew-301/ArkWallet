using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccessSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    IsGlobalAccessEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WhiteList = table.Column<string>(type: "text", nullable: false),
                    BlackList = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AppStates",
                columns: table => new
                {
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppStates", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "CharacterTokens",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Rarity = table.Column<int>(type: "integer", nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalSupply = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    IconUrl = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterTokens", x => x.Symbol);
                });

            migrationBuilder.CreateTable(
                name: "MarketMakerBots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "text", nullable: false),
                    TraderId = table.Column<long>(type: "bigint", nullable: false),
                    BasePower = table.Column<decimal>(type: "numeric", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    NextPowerChange = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextRebalance = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketMakerBots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MiningMachines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    SwitchingTime = table.Column<int>(type: "integer", nullable: false),
                    Reusability = table.Column<decimal>(type: "numeric", nullable: false),
                    IsActiveForSale = table.Column<bool>(type: "boolean", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric", nullable: false),
                    Efficiency = table.Column<decimal>(type: "numeric", nullable: false),
                    Image = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiningMachines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Traders",
                columns: table => new
                {
                    TelegramId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "text", nullable: true),
                    Balance = table.Column<decimal>(type: "numeric", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NotificationOn = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traders", x => x.TelegramId);
                });

            migrationBuilder.CreateTable(
                name: "MiningGlobalRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TokenId = table.Column<string>(type: "text", nullable: false),
                    CurrentCoefficient = table.Column<decimal>(type: "numeric", nullable: false),
                    FutureCoefficient = table.Column<decimal>(type: "numeric", nullable: false),
                    BaseTokenMiningSpeed = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiningGlobalRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MiningGlobalRules_CharacterTokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "CharacterTokens",
                        principalColumn: "Symbol",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PriceCandles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OpenPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    HighPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    LowPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    ClosePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceCandles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceCandles_CharacterTokens_CharacterTokenId",
                        column: x => x.CharacterTokenId,
                        principalTable: "CharacterTokens",
                        principalColumn: "Symbol",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MiningMachineRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MiningMachineId = table.Column<long>(type: "bigint", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "text", nullable: false),
                    MiningCoefficient = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiningMachineRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MiningMachineRules_CharacterTokens_CharacterTokenId",
                        column: x => x.CharacterTokenId,
                        principalTable: "CharacterTokens",
                        principalColumn: "Symbol",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MiningMachineRules_MiningMachines_MiningMachineId",
                        column: x => x.MiningMachineId,
                        principalTable: "MiningMachines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BalanceSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TotalBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    MainBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    LongOrderReserveBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    ShortOrderReserveBalance = table.Column<decimal>(type: "numeric", nullable: false),
                    BalanceInTokens = table.Column<decimal>(type: "numeric", nullable: false),
                    SnapshotDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TraderId = table.Column<long>(type: "bigint", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "PortfolioItems",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    TraderTelegramId = table.Column<long>(type: "bigint", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "text", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    SellingQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReserveQuantity = table.Column<int>(type: "integer", nullable: false),
                    AverageBuyPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    AverageSellPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    AverageReservePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    Id = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "text", nullable: false),
                    TraderTelegramId = table.Column<long>(type: "bigint", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    AverageExecutePrice = table.Column<decimal>(type: "numeric", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    FilledQuantity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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
                    Id = table.Column<string>(type: "text", nullable: false),
                    BuyerId = table.Column<long>(type: "bigint", nullable: false),
                    SellerId = table.Column<long>(type: "bigint", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "MiningMachineSlots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TraderId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    SwitchingTime = table.Column<int>(type: "integer", nullable: false),
                    Efficiency = table.Column<decimal>(type: "numeric", nullable: false),
                    Image = table.Column<string>(type: "text", nullable: false),
                    TokenId = table.Column<string>(type: "text", nullable: true),
                    MiningGlobalRuleId = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartSwitchingDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndSwitchingDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TokensAmountCollected = table.Column<decimal>(type: "numeric", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SoldAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiningMachineSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MiningMachineSlots_CharacterTokens_TokenId",
                        column: x => x.TokenId,
                        principalTable: "CharacterTokens",
                        principalColumn: "Symbol",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MiningMachineSlots_MiningGlobalRules_MiningGlobalRuleId",
                        column: x => x.MiningGlobalRuleId,
                        principalTable: "MiningGlobalRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MiningMachineSlots_Traders_TraderId",
                        column: x => x.TraderId,
                        principalTable: "Traders",
                        principalColumn: "TelegramId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MiningMachineSlotRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MiningMachineSlotId = table.Column<long>(type: "bigint", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "text", nullable: false),
                    MiningCoefficient = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiningMachineSlotRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MiningMachineSlotRules_CharacterTokens_CharacterTokenId",
                        column: x => x.CharacterTokenId,
                        principalTable: "CharacterTokens",
                        principalColumn: "Symbol",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MiningMachineSlotRules_MiningMachineSlots_MiningMachineSlot~",
                        column: x => x.MiningMachineSlotId,
                        principalTable: "MiningMachineSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceSnapshots_TraderId_SnapshotDateTime",
                table: "BalanceSnapshots",
                columns: new[] { "TraderId", "SnapshotDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_MiningGlobalRules_TokenId",
                table: "MiningGlobalRules",
                column: "TokenId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineRules_CharacterTokenId",
                table: "MiningMachineRules",
                column: "CharacterTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineRules_MiningMachineId_CharacterTokenId",
                table: "MiningMachineRules",
                columns: new[] { "MiningMachineId", "CharacterTokenId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachines_Name",
                table: "MiningMachines",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlotRules_CharacterTokenId",
                table: "MiningMachineSlotRules",
                column: "CharacterTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlotRules_MiningMachineSlotId_CharacterTokenId",
                table: "MiningMachineSlotRules",
                columns: new[] { "MiningMachineSlotId", "CharacterTokenId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlots_MiningGlobalRuleId",
                table: "MiningMachineSlots",
                column: "MiningGlobalRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlots_Status",
                table: "MiningMachineSlots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlots_TokenId",
                table: "MiningMachineSlots",
                column: "TokenId");

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlots_TraderId",
                table: "MiningMachineSlots",
                column: "TraderId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_CharacterTokenId",
                table: "PortfolioItems",
                column: "CharacterTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioItems_TraderTelegramId",
                table: "PortfolioItems",
                column: "TraderTelegramId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceCandles_CharacterTokenId",
                table: "PriceCandles",
                column: "CharacterTokenId");

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
                name: "AccessSettings");

            migrationBuilder.DropTable(
                name: "AppStates");

            migrationBuilder.DropTable(
                name: "BalanceSnapshots");

            migrationBuilder.DropTable(
                name: "MarketMakerBots");

            migrationBuilder.DropTable(
                name: "MiningMachineRules");

            migrationBuilder.DropTable(
                name: "MiningMachineSlotRules");

            migrationBuilder.DropTable(
                name: "PortfolioItems");

            migrationBuilder.DropTable(
                name: "PriceCandles");

            migrationBuilder.DropTable(
                name: "TradeOrders");

            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "MiningMachines");

            migrationBuilder.DropTable(
                name: "MiningMachineSlots");

            migrationBuilder.DropTable(
                name: "MiningGlobalRules");

            migrationBuilder.DropTable(
                name: "Traders");

            migrationBuilder.DropTable(
                name: "CharacterTokens");
        }
    }
}
