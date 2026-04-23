using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDealFinderDeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DealFinderDeals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Asin = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Brand = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    AmazonLikeNewPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    AmazonNewPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    EbayAvgSoldPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    EbayCurrentListingPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    EbaySoldCount30Days = table.Column<int>(type: "INTEGER", nullable: false),
                    EbayFees = table.Column<decimal>(type: "TEXT", nullable: false),
                    ShippingCost = table.Column<decimal>(type: "TEXT", nullable: false),
                    Profit = table.Column<decimal>(type: "TEXT", nullable: false),
                    Roi = table.Column<decimal>(type: "TEXT", nullable: false),
                    SalesRank = table.Column<int>(type: "INTEGER", nullable: false),
                    SalesRankDrops30 = table.Column<int>(type: "INTEGER", nullable: false),
                    BoughtLastMonth = table.Column<int>(type: "INTEGER", nullable: false),
                    Rating = table.Column<decimal>(type: "TEXT", nullable: false),
                    ReviewCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SellerType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SellerCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PriceVariation90To30 = table.Column<decimal>(type: "TEXT", nullable: false),
                    PriceVariationPct90To30 = table.Column<decimal>(type: "TEXT", nullable: false),
                    AmazonUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DealFinderDeals", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0001-0000-0000-000000000001",
                column: "ConcurrencyStamp",
                value: "d22e2d99-d596-49ec-8fa5-37440da8a073");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0002-0000-0000-000000000002",
                column: "ConcurrencyStamp",
                value: "927c2633-c311-4aa0-9527-557e281383a5");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0003-0000-0000-000000000003",
                column: "ConcurrencyStamp",
                value: "36e01485-420b-406c-a422-23a3eab0b970");

            migrationBuilder.UpdateData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "a1b2c3d4-0004-0000-0000-000000000004",
                column: "ConcurrencyStamp",
                value: "babce00a-bde3-4466-8280-320fde8d679f");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DealFinderDeals");

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
        }
    }
}
