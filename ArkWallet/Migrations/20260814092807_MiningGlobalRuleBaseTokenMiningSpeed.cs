using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class MiningGlobalRuleBaseTokenMiningSpeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BaseMiningSpeed",
                table: "MiningGlobalRules",
                newName: "BaseTokenMiningSpeed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BaseTokenMiningSpeed",
                table: "MiningGlobalRules",
                newName: "BaseMiningSpeed");
        }
    }
}
