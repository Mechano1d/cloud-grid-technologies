using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OnlineBanking.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFailureMetadataAndClientIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "failure_code",
                schema: "transactions",
                table: "transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failure_error_type",
                schema: "transactions",
                table: "transactions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_created_at_utc",
                schema: "clients",
                table: "clients",
                column: "created_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_clients_created_at_utc",
                schema: "clients",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "failure_code",
                schema: "transactions",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "failure_error_type",
                schema: "transactions",
                table: "transactions");
        }
    }
}
