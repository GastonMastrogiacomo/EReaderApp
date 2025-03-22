using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EReaderApp.Migrations
{
    /// <inheritdoc />
    public partial class NotesUserFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notes_Users_UserIdUser",
                table: "Notes");

            migrationBuilder.DropIndex(
                name: "IX_Notes_UserIdUser",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "UserIdUser",
                table: "Notes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserIdUser",
                table: "Notes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Notes_UserIdUser",
                table: "Notes",
                column: "UserIdUser");

            migrationBuilder.AddForeignKey(
                name: "FK_Notes_Users_UserIdUser",
                table: "Notes",
                column: "UserIdUser",
                principalTable: "Users",
                principalColumn: "IdUser",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
