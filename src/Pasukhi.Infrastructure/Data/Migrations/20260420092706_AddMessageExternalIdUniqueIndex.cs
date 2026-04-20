using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pasukhi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageExternalIdUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_BusinessId",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_BusinessId_ExternalMessageId",
                table: "Messages",
                columns: new[] { "BusinessId", "ExternalMessageId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Messages_BusinessId_ExternalMessageId",
                table: "Messages");

            migrationBuilder.CreateIndex(
                name: "IX_Messages_BusinessId",
                table: "Messages",
                column: "BusinessId");
        }
    }
}
