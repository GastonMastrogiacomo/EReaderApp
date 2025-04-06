using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EReaderApp.Migrations
{
    /// <inheritdoc />
    public partial class BookModelUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subtitle",
                table: "Books");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Subtitle",
                table: "Books",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
