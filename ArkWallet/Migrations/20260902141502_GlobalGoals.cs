using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class GlobalGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalGoals",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Target = table.Column<decimal>(type: "numeric", nullable: false),
                    Actual = table.Column<decimal>(type: "numeric", nullable: false),
                    Progress = table.Column<decimal>(type: "numeric", nullable: false),
                    AchievedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalGoals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlobalGoalHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoalId = table.Column<long>(type: "bigint", nullable: false),
                    AchievedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Target = table.Column<decimal>(type: "numeric", nullable: false),
                    SymbolForReward = table.Column<string>(type: "text", nullable: false),
                    AmountForReward = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalGoalHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlobalGoalHistories_GlobalGoals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "GlobalGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GlobalGoalSteps",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GoalId = table.Column<long>(type: "bigint", nullable: false),
                    StepNumber = table.Column<int>(type: "integer", nullable: false),
                    Target = table.Column<decimal>(type: "numeric", nullable: false),
                    SymbolForReward = table.Column<string>(type: "text", nullable: false),
                    AmountForReward = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalGoalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlobalGoalSteps_GlobalGoals_GoalId",
                        column: x => x.GoalId,
                        principalTable: "GlobalGoals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalGoalHistories_GoalId",
                table: "GlobalGoalHistories",
                column: "GoalId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalGoals_Name",
                table: "GlobalGoals",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GlobalGoalSteps_GoalId_StepNumber",
                table: "GlobalGoalSteps",
                columns: new[] { "GoalId", "StepNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalGoalHistories");

            migrationBuilder.DropTable(
                name: "GlobalGoalSteps");

            migrationBuilder.DropTable(
                name: "GlobalGoals");
        }
    }
}
