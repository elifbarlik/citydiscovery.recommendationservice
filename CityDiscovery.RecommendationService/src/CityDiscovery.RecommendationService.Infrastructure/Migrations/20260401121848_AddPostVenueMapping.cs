using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CityDiscovery.RecommendationService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostVenueMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PostVenueMappings",
                columns: table => new
                {
                    PostId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostVenueMappings", x => x.PostId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PostVenueMappings_VenueId",
                table: "PostVenueMappings",
                column: "VenueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PostVenueMappings");
        }
    }
}
