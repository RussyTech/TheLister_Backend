using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcingDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SourcingDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", nullable: false),
                    FileContent = table.Column<byte[]>(type: "BLOB", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ProductCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourcingDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourcingDocuments_AspNetUsers_UserId",
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
                value: "e7d13937-cade-4bc8-aecc-8dc3c3ac54cb");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0002-0000-0000-000000000002",
                column: "ConcurrencyStamp",
                value: "ad171c2a-3111-4dc8-8cb2-e7ae0e96eeb8");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0003-0000-0000-000000000003",
                column: "ConcurrencyStamp",
                value: "36226ae6-d1da-43da-a37d-eb21f74447c7");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0004-0000-0000-000000000004",
                column: "ConcurrencyStamp",
                value: "522c77c2-b389-4068-9071-8eca627ea1b1");

            migrationBuilder.CreateIndex(
                name: "IX_SourcingDocuments_UserId",
                table: "SourcingDocuments",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SourcingDocuments");

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
        }
    }
}
