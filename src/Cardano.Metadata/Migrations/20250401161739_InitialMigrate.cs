using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cardano.Metadata.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigrate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncState",
                columns: table => new
                {
                    Hash = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncState", x => x.Hash);
                });

            migrationBuilder.CreateTable(
                name: "TokenMetadata",
                columns: table => new
                {
                    Subject = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Ticker = table.Column<string>(type: "text", nullable: false),
                    Policy = table.Column<string>(type: "text", nullable: false),
                    Decimals = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: true),
                    Logo = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokenMetadata", x => x.Subject);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokenMetadata_Name_Description_Ticker",
                table: "TokenMetadata",
                columns: new[] { "Name", "Description", "Ticker" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncState");

            migrationBuilder.DropTable(
                name: "TokenMetadata");
        }
    }
}
