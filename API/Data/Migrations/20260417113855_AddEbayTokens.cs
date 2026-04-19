using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEbayTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EbayTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    AccessToken = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshToken = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RefreshTokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EbayUsername = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EbayTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EbayTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0001-0000-0000-000000000001",
                column: "ConcurrencyStamp",
                value: "8d9f398c-399e-4619-918e-9ce68051c633");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0002-0000-0000-000000000002",
                column: "ConcurrencyStamp",
                value: "7faadc3d-03c6-4296-9d7f-aa12d9746126");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0003-0000-0000-000000000003",
                column: "ConcurrencyStamp",
                value: "6c0f57cb-a720-4251-b1d4-9e3031702ab8");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0004-0000-0000-000000000004",
                column: "ConcurrencyStamp",
                value: "6ae7414d-0c74-4d91-9adc-59f352c76c5a");

            migrationBuilder.CreateIndex(
                name: "IX_EbayTokens_UserId",
                table: "EbayTokens",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EbayTokens");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0001-0000-0000-000000000001",
                column: "ConcurrencyStamp",
                value: "2c024f90-5f3e-436d-b105-402b86864245");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0002-0000-0000-000000000002",
                column: "ConcurrencyStamp",
                value: "9063fa93-e2a9-465d-a87a-95b39f1eba3b");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0003-0000-0000-000000000003",
                column: "ConcurrencyStamp",
                value: "f5c99d8c-051d-44dc-bcca-756d8270321a");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0004-0000-0000-000000000004",
                column: "ConcurrencyStamp",
                value: "b39a2029-019d-444f-998f-68601bd45286");
        }
    }
}
