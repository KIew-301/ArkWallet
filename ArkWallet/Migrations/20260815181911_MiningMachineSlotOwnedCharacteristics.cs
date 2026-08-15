using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class MiningMachineSlotOwnedCharacteristics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Efficiency",
                table: "MiningMachineSlots",
                type: "TEXT",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Image",
                table: "MiningMachineSlots",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "MiningMachineSlots",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "MiningMachineSlots",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SwitchingTime",
                table: "MiningMachineSlots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MiningMachineSlotRules",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MiningMachineSlotId = table.Column<long>(type: "INTEGER", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "TEXT", nullable: false),
                    MiningCoefficient = table.Column<decimal>(type: "TEXT", nullable: false)
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
                        name: "FK_MiningMachineSlotRules_MiningMachineSlots_MiningMachineSlotId",
                        column: x => x.MiningMachineSlotId,
                        principalTable: "MiningMachineSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Перенос характеристик из каталога машин в слоты (столбец MiningMachineId ещё существует)
            migrationBuilder.Sql(
                """
                UPDATE "MiningMachineSlots"
                SET "Name" = (SELECT "Name" FROM "MiningMachines" m WHERE m."Id" = "MiningMachineSlots"."MiningMachineId"),
                    "Type" = (SELECT "Type" FROM "MiningMachines" m WHERE m."Id" = "MiningMachineSlots"."MiningMachineId"),
                    "SwitchingTime" = (SELECT "SwitchingTime" FROM "MiningMachines" m WHERE m."Id" = "MiningMachineSlots"."MiningMachineId"),
                    "Efficiency" = (SELECT "Efficiency" FROM "MiningMachines" m WHERE m."Id" = "MiningMachineSlots"."MiningMachineId"),
                    "Image" = (SELECT "Image" FROM "MiningMachines" m WHERE m."Id" = "MiningMachineSlots"."MiningMachineId")
                WHERE EXISTS (SELECT 1 FROM "MiningMachines" m WHERE m."Id" = "MiningMachineSlots"."MiningMachineId");
                """);

            // Копирование правил машины в правила слота
            migrationBuilder.Sql(
                """
                INSERT INTO "MiningMachineSlotRules" ("MiningMachineSlotId", "CharacterTokenId", "MiningCoefficient")
                SELECT s."Id", r."CharacterTokenId", r."MiningCoefficient"
                FROM "MiningMachineSlots" s
                INNER JOIN "MiningMachineRules" r ON r."MiningMachineId" = s."MiningMachineId";
                """);

            // Удаление старых колонок и внешних ключей (SQLite перестроит таблицу автоматически)
            migrationBuilder.DropForeignKey(
                name: "FK_MiningMachineSlots_MiningMachineRules_MachineRuleId",
                table: "MiningMachineSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_MiningMachineSlots_MiningMachines_MiningMachineId",
                table: "MiningMachineSlots");

            migrationBuilder.DropIndex(
                name: "IX_MiningMachineSlots_MachineRuleId",
                table: "MiningMachineSlots");

            migrationBuilder.DropIndex(
                name: "IX_MiningMachineSlots_MiningMachineId",
                table: "MiningMachineSlots");

            migrationBuilder.DropColumn(
                name: "MachineRuleId",
                table: "MiningMachineSlots");

            migrationBuilder.DropColumn(
                name: "MiningMachineId",
                table: "MiningMachineSlots");

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlotRules_CharacterTokenId",
                table: "MiningMachineSlotRules",
                column: "CharacterTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlotRules_MiningMachineSlotId_CharacterTokenId",
                table: "MiningMachineSlotRules",
                columns: new[] { "MiningMachineSlotId", "CharacterTokenId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MiningMachineSlotRules");

            migrationBuilder.DropColumn(
                name: "Efficiency",
                table: "MiningMachineSlots");

            migrationBuilder.DropColumn(
                name: "Image",
                table: "MiningMachineSlots");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "MiningMachineSlots");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "MiningMachineSlots");

            migrationBuilder.DropColumn(
                name: "SwitchingTime",
                table: "MiningMachineSlots");

            migrationBuilder.AddColumn<long>(
                name: "MiningMachineId",
                table: "MiningMachineSlots",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "MachineRuleId",
                table: "MiningMachineSlots",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlots_MachineRuleId",
                table: "MiningMachineSlots",
                column: "MachineRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_MiningMachineSlots_MiningMachineId",
                table: "MiningMachineSlots",
                column: "MiningMachineId");

            migrationBuilder.AddForeignKey(
                name: "FK_MiningMachineSlots_MiningMachineRules_MachineRuleId",
                table: "MiningMachineSlots",
                column: "MachineRuleId",
                principalTable: "MiningMachineRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MiningMachineSlots_MiningMachines_MiningMachineId",
                table: "MiningMachineSlots",
                column: "MiningMachineId",
                principalTable: "MiningMachines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
