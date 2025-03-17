using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EReaderApp.Migrations
{
    /// <inheritdoc />
    public partial class FlipBookFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookmarks_Books_BookId",
                table: "Bookmarks");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookmarks_Users_UserId",
                table: "Bookmarks");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadingActivity_Books_BookId",
                table: "ReadingActivity");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadingActivity_Users_UserId",
                table: "ReadingActivity");

            migrationBuilder.DropTable(
                name: "ReadingProgress");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Bookmarks",
                table: "Bookmarks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReadingActivity",
                table: "ReadingActivity");

            migrationBuilder.DropColumn(
                name: "LongestSessionMinutes",
                table: "ReadingActivity");

            migrationBuilder.RenameTable(
                name: "Bookmarks",
                newName: "BookMarks");

            migrationBuilder.RenameTable(
                name: "ReadingActivity",
                newName: "ReadingActivities");

            migrationBuilder.RenameIndex(
                name: "IX_Bookmarks_UserId",
                table: "BookMarks",
                newName: "IX_BookMarks_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookmarks_BookId",
                table: "BookMarks",
                newName: "IX_BookMarks_BookId");

            migrationBuilder.RenameColumn(
                name: "LastAccessedAt",
                table: "ReadingActivities",
                newName: "LastAccess");

            migrationBuilder.RenameColumn(
                name: "FirstAccessedAt",
                table: "ReadingActivities",
                newName: "FirstAccess");

            migrationBuilder.RenameIndex(
                name: "IX_ReadingActivity_UserId",
                table: "ReadingActivities",
                newName: "IX_ReadingActivities_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ReadingActivity_BookId",
                table: "ReadingActivities",
                newName: "IX_ReadingActivities_BookId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BookMarks",
                table: "BookMarks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReadingActivities",
                table: "ReadingActivities",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ReadingStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BookId = table.Column<int>(type: "int", nullable: false),
                    CurrentPage = table.Column<int>(type: "int", nullable: false),
                    ZoomLevel = table.Column<float>(type: "real", nullable: false),
                    ViewMode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastAccessed = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingStates_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "IdBook",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "IdUser");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingStates_BookId",
                table: "ReadingStates",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingStates_UserId",
                table: "ReadingStates",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_BookMarks_Books_BookId",
                table: "BookMarks",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "IdBook",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_BookMarks_Users_UserId",
                table: "BookMarks",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "IdUser");

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingActivities_Books_BookId",
                table: "ReadingActivities",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "IdBook",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingActivities_Users_UserId",
                table: "ReadingActivities",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "IdUser");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BookMarks_Books_BookId",
                table: "BookMarks");

            migrationBuilder.DropForeignKey(
                name: "FK_BookMarks_Users_UserId",
                table: "BookMarks");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadingActivities_Books_BookId",
                table: "ReadingActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_ReadingActivities_Users_UserId",
                table: "ReadingActivities");

            migrationBuilder.DropTable(
                name: "ReadingStates");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BookMarks",
                table: "BookMarks");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ReadingActivities",
                table: "ReadingActivities");

            migrationBuilder.RenameTable(
                name: "BookMarks",
                newName: "Bookmarks");

            migrationBuilder.RenameTable(
                name: "ReadingActivities",
                newName: "ReadingActivity");

            migrationBuilder.RenameIndex(
                name: "IX_BookMarks_UserId",
                table: "Bookmarks",
                newName: "IX_Bookmarks_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_BookMarks_BookId",
                table: "Bookmarks",
                newName: "IX_Bookmarks_BookId");

            migrationBuilder.RenameColumn(
                name: "LastAccess",
                table: "ReadingActivity",
                newName: "LastAccessedAt");

            migrationBuilder.RenameColumn(
                name: "FirstAccess",
                table: "ReadingActivity",
                newName: "FirstAccessedAt");

            migrationBuilder.RenameIndex(
                name: "IX_ReadingActivities_UserId",
                table: "ReadingActivity",
                newName: "IX_ReadingActivity_UserId");

            migrationBuilder.RenameIndex(
                name: "IX_ReadingActivities_BookId",
                table: "ReadingActivity",
                newName: "IX_ReadingActivity_BookId");

            migrationBuilder.AddColumn<int>(
                name: "LongestSessionMinutes",
                table: "ReadingActivity",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Bookmarks",
                table: "Bookmarks",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ReadingActivity",
                table: "ReadingActivity",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ReadingProgress",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BookId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CompletionPercentage = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentPage = table.Column<int>(type: "int", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false),
                    LastReadAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalPages = table.Column<int>(type: "int", nullable: false),
                    TotalReadingTimeMinutes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadingProgress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadingProgress_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "IdBook",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadingProgress_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "IdUser",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReadingProgress_BookId",
                table: "ReadingProgress",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadingProgress_UserId",
                table: "ReadingProgress",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_Books_BookId",
                table: "Bookmarks",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "IdBook",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookmarks_Users_UserId",
                table: "Bookmarks",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "IdUser",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingActivity_Books_BookId",
                table: "ReadingActivity",
                column: "BookId",
                principalTable: "Books",
                principalColumn: "IdBook",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ReadingActivity_Users_UserId",
                table: "ReadingActivity",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "IdUser",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
