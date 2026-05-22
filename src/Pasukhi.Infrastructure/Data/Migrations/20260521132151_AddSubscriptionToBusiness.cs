using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pasukhi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionToBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodEnd",
                table: "Businesses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeCustomerId",
                table: "Businesses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Businesses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionStatus",
                table: "Businesses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Tier",
                table: "Businesses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_StripeCustomerId",
                table: "Businesses",
                column: "StripeCustomerId",
                unique: true,
                filter: "\"StripeCustomerId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPeriodEnd",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "StripeCustomerId",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "SubscriptionStatus",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "Tier",
                table: "Businesses");

            migrationBuilder.DropIndex(
                name: "IX_Businesses_StripeCustomerId",
                table: "Businesses");
        }
    }
}
