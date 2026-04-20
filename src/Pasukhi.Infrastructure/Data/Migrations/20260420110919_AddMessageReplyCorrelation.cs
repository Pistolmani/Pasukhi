using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pasukhi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageReplyCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "Messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_BusinessId_ReplyToMessageId_Source",
                table: "Messages",
                columns: new[] { "BusinessId", "ReplyToMessageId", "Source" },
                unique: true,
                filter: "\"ReplyToMessageId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_BusinessId_ReplyToMessageId_Source",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "Messages");
        }
    }
}
