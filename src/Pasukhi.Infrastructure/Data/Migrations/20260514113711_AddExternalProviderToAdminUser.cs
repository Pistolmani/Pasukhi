using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pasukhi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalProviderToAdminUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalProvider",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalProviderId",
                table: "AspNetUsers",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_ExternalProvider_ExternalProviderId",
                table: "AspNetUsers",
                columns: new[] { "ExternalProvider", "ExternalProviderId" },
                unique: true,
                filter: "\"ExternalProvider\" IS NOT NULL AND \"ExternalProviderId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_ExternalProvider_ExternalProviderId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ExternalProvider",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ExternalProviderId",
                table: "AspNetUsers");
        }
    }
}
