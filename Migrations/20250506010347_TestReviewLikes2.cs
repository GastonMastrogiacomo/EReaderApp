using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EReaderApp.Migrations
{
    /// <inheritdoc />
    public partial class TestReviewLikes2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastPageRead",
                table: "ReadingActivities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalPagesRead",
                table: "ReadingActivities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalReadingTimeMinutes",
                table: "ReadingActivities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ReviewLikes",
                columns: table => new
                {
                    FKIdUser = table.Column<int>(type: "int", nullable: false),
                    FKIdReview = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewLikes", x => new { x.FKIdUser, x.FKIdReview });
                    table.ForeignKey(
                        name: "FK_ReviewLikes_Reviews_FKIdReview",
                        column: x => x.FKIdReview,
                        principalTable: "Reviews",
                        principalColumn: "IdReview");
                    table.ForeignKey(
                        name: "FK_ReviewLikes_Users_FKIdUser",
                        column: x => x.FKIdUser,
                        principalTable: "Users",
                        principalColumn: "IdUser");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewLikes_FKIdReview",
                table: "ReviewLikes",
                column: "FKIdReview");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewLikes");

            migrationBuilder.DropColumn(
                name: "LastPageRead",
                table: "ReadingActivities");

            migrationBuilder.DropColumn(
                name: "TotalPagesRead",
                table: "ReadingActivities");

            migrationBuilder.DropColumn(
                name: "TotalReadingTimeMinutes",
                table: "ReadingActivities");
        }
    }
}
