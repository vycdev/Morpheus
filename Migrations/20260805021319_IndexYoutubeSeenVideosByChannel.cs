using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Morpheus.Migrations
{
    /// <inheritdoc />
    public partial class IndexYoutubeSeenVideosByChannel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_YoutubeSeenVideos_YoutubeChannelId",
                table: "YoutubeSeenVideos",
                column: "YoutubeChannelId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_YoutubeSeenVideos_YoutubeChannelId",
                table: "YoutubeSeenVideos");
        }
    }
}
