using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityDiscovery.RecommendationService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2Features : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DismissedVenues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DismissedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DismissedVenues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PreferredCategories = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPreferences", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DismissedVenues_UserId",
                table: "DismissedVenues",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_DismissedVenues_UserId_VenueId",
                table: "DismissedVenues",
                columns: new[] { "UserId", "VenueId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DismissedVenues");

            migrationBuilder.DropTable(
                name: "UserPreferences");
        }
    }
}
