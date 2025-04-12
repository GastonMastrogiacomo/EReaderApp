using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EReaderApp.Migrations
{
    /// <inheritdoc />
    public partial class BookModelChangesAuthorBio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuthorBio",
                table: "Books",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthorBio",
                table: "Books");
        }
    }
}
