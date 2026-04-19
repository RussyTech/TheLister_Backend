using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddListingConditionAndDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Condition",
                table: "EbayListings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ListingDuration",
                table: "EbayListings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Condition",
                table: "EbayListings");

            migrationBuilder.DropColumn(
                name: "ListingDuration",
                table: "EbayListings");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0001-0000-0000-000000000001",
                column: "ConcurrencyStamp",
                value: "fcf80067-9464-4f2a-a6fe-d01377182c66");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0002-0000-0000-000000000002",
                column: "ConcurrencyStamp",
                value: "49f57f64-e2e9-41d3-b30f-d2dd5f6a23ba");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0003-0000-0000-000000000003",
                column: "ConcurrencyStamp",
                value: "a08148fc-9c5e-458a-850c-3f2634fc5fe1");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0004-0000-0000-000000000004",
                column: "ConcurrencyStamp",
                value: "f5ba48d5-d0d9-43a4-89e6-75a10601f62a");
        }
    }
}
