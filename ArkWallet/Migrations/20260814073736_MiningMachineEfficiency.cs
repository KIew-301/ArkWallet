using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class MiningMachineEfficiency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Efficiency",
                table: "MiningMachines",
                type: "TEXT",
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachines_Name",
                table: "MiningMachines",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MiningMachines_Name",
                table: "MiningMachines");

            migrationBuilder.DropColumn(
                name: "Efficiency",
                table: "MiningMachines");
        }
    }
}
