using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pasukhi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBotReadinessTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BotKnowledgeSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceQuestionKeys = table.Column<List<string>>(type: "jsonb", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    DedupeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotKnowledgeSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BotKnowledgeSuggestions_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BotQuestionnaireAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AnswerText = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    IsSkipped = table.Column<bool>(type: "boolean", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BotQuestionnaireAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BotQuestionnaireAnswers_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BotKnowledgeSuggestions_BusinessId_Status",
                table: "BotKnowledgeSuggestions",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BotKnowledgeSuggestions_BusinessId_Type_DedupeHash",
                table: "BotKnowledgeSuggestions",
                columns: new[] { "BusinessId", "Type", "DedupeHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BotQuestionnaireAnswers_BusinessId_QuestionKey",
                table: "BotQuestionnaireAnswers",
                columns: new[] { "BusinessId", "QuestionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BotKnowledgeSuggestions");

            migrationBuilder.DropTable(
                name: "BotQuestionnaireAnswers");
        }
    }
}
