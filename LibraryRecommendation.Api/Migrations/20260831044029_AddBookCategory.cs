using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LibraryRecommendation.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBookCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Books",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Every book that predates this column came from the CMU fiction import.
            migrationBuilder.Sql("UPDATE [Books] SET [Category] = N'Fiction';");

            migrationBuilder.CreateIndex(
                name: "IX_Books_Category",
                table: "Books",
                column: "Category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Books_Category",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Books");
        }
    }
}
