using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class MiningSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MiningGlobalRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TokenId = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentCoefficient = table.Column<decimal>(type: "TEXT", nullable: false),
                    FutureCoefficient = table.Column<decimal>(type: "TEXT", nullable: false),
                    BaseMiningSpeed = table.Column<decimal>(type: "TEXT", nullable: false)
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
                name: "MiningMachines",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: false),
                    SwitchingTime = table.Column<int>(type: "INTEGER", nullable: false),
                    Reusability = table.Column<decimal>(type: "TEXT", nullable: false),
                    IsActiveForSale = table.Column<bool>(type: "INTEGER", nullable: false),
                    Cost = table.Column<decimal>(type: "TEXT", nullable: false),
                    Image = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MiningMachines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MiningMachineRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MiningMachineId = table.Column<long>(type: "INTEGER", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "TEXT", nullable: false),
                    MiningCoefficient = table.Column<decimal>(type: "TEXT", nullable: false)
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
                name: "MiningMachineSlots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TraderId = table.Column<long>(type: "INTEGER", nullable: false),
                    MiningMachineId = table.Column<long>(type: "INTEGER", nullable: false),
                    TokenId = table.Column<string>(type: "TEXT", nullable: true),
                    MachineRuleId = table.Column<long>(type: "INTEGER", nullable: true),
                    MiningGlobalRuleId = table.Column<long>(type: "INTEGER", nullable: true),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    StartSwitchingDateTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndSwitchingDateTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    TokensAmountCollected = table.Column<decimal>(type: "TEXT", nullable: false),
                    Cost = table.Column<decimal>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SoldAt = table.Column<DateTime>(type: "TEXT", nullable: true)
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
                        name: "FK_MiningMachineSlots_MiningMachineRules_MachineRuleId",
                        column: x => x.MachineRuleId,
                        principalTable: "MiningMachineRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MiningMachineSlots_MiningMachines_MiningMachineId",
                        column: x => x.MiningMachineId,
                        principalTable: "MiningMachines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MiningMachineSlots_Traders_TraderId",
                        column: x => x.TraderId,
                        principalTable: "Traders",
                        principalColumn: "TelegramId",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "IX_MiningMachineSlots_MachineRuleId",
                table: "MiningMachineSlots",
                column: "MachineRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlots_MiningGlobalRuleId",
                table: "MiningMachineSlots",
                column: "MiningGlobalRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlots_MiningMachineId",
                table: "MiningMachineSlots",
                column: "MiningMachineId");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MiningMachineSlots");

            migrationBuilder.DropTable(
                name: "MiningGlobalRules");

            migrationBuilder.DropTable(
                name: "MiningMachineRules");

            migrationBuilder.DropTable(
                name: "MiningMachines");
        }
    }
}
