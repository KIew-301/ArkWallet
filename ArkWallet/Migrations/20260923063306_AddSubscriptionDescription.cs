using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ArkWallet.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SavedPaymentMethodId",
                table: "Traders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Subscriptions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SubscriptionPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TraderId = table.Column<long>(type: "bigint", nullable: false),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<int>(type: "integer", nullable: false),
                    AmountRubles = table.Column<decimal>(type: "numeric", nullable: false),
                    ExternalPaymentId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ConfirmationUrl = table.Column<string>(type: "text", nullable: true),
                    PaymentMethodId = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SucceededAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CanceledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPayments", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionPayments");

            migrationBuilder.DropColumn(
                name: "SavedPaymentMethodId",
                table: "Traders");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Subscriptions");
        }
    }
}
