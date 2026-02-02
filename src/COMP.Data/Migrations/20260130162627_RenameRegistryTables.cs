using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comp.Migrations
{
    /// <inheritdoc />
    public partial class RenameRegistryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename tables
            migrationBuilder.RenameTable(
                name: "SyncState",
                schema: "public",
                newName: "RegistrySyncState",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "TokenMetadata",
                schema: "public",
                newName: "TokenMetadataRegistry",
                newSchema: "public");

            // Rename primary key constraints
            migrationBuilder.RenameIndex(
                name: "PK_SyncState",
                schema: "public",
                table: "RegistrySyncState",
                newName: "PK_RegistrySyncState");

            migrationBuilder.RenameIndex(
                name: "PK_TokenMetadata",
                schema: "public",
                table: "TokenMetadataRegistry",
                newName: "PK_TokenMetadataRegistry");

            // Rename existing index
            migrationBuilder.RenameIndex(
                name: "IX_TokenMetadata_Name_Description_Ticker",
                schema: "public",
                table: "TokenMetadataRegistry",
                newName: "IX_TokenMetadataRegistry_Name_Description_Ticker");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rename tables back
            migrationBuilder.RenameTable(
                name: "RegistrySyncState",
                schema: "public",
                newName: "SyncState",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "TokenMetadataRegistry",
                schema: "public",
                newName: "TokenMetadata",
                newSchema: "public");

            // Rename primary key constraints back
            migrationBuilder.RenameIndex(
                name: "PK_RegistrySyncState",
                schema: "public",
                table: "SyncState",
                newName: "PK_SyncState");

            migrationBuilder.RenameIndex(
                name: "PK_TokenMetadataRegistry",
                schema: "public",
                table: "TokenMetadata",
                newName: "PK_TokenMetadata");

            // Rename index back
            migrationBuilder.RenameIndex(
                name: "IX_TokenMetadataRegistry_Name_Description_Ticker",
                schema: "public",
                table: "TokenMetadata",
                newName: "IX_TokenMetadata_Name_Description_Ticker");
        }
    }
}
