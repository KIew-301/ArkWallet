using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class NewMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PriceCandles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OpenPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    HighPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    LowPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    ClosePrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CharacterTokenId = table.Column<string>(type: "TEXT", nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "IX_PriceCandles_CharacterTokenId",
                table: "PriceCandles",
                column: "CharacterTokenId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceCandles");
        }
    }
}
