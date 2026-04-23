using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace API.Entities.DealFinder
{
    public class DealFinderDeal
{
    public int Id { get; set; }
    [MaxLength(20)]  public string Asin         { get; set; } = "";
    [MaxLength(500)] public string Title        { get; set; } = "";
    [MaxLength(1000)]public string? ImageUrl    { get; set; }
    [MaxLength(200)] public string? Category    { get; set; }
    [MaxLength(200)] public string? Brand       { get; set; }
    // Amazon pricing (GBP, incl. VAT)
    public decimal AmazonLikeNewPrice   { get; set; }
    public decimal AmazonNewPrice       { get; set; }
    // eBay pricing
    public decimal EbayAvgSoldPrice         { get; set; }
    public decimal EbayCurrentListingPrice  { get; set; }
    public int     EbaySoldCount30Days      { get; set; }
    // Profit (UK model)
    public decimal EbayFees          { get; set; }   // FVF 12.8% + £0.30
    public decimal ShippingCost      { get; set; }   // default £3.50
    public decimal Profit            { get; set; }
    public decimal Roi               { get; set; }   // %
    // Amazon signals
    public int     SalesRank         { get; set; }
    public int     SalesRankDrops30  { get; set; }   // BSR drops last 30 days
    public int     BoughtLastMonth   { get; set; }
    public decimal Rating            { get; set; }
    public int     ReviewCount       { get; set; }
    // Seller info
    [MaxLength(50)] public string SellerType  { get; set; } = "";  // Amazon/FBA/FBM
    public int SellerCount { get; set; }
    // Price history (Keepa)
    public decimal PriceVariation90To30    { get; set; }
    public decimal PriceVariationPct90To30 { get; set; }
    [MaxLength(500)] public string AmazonUrl { get; set; } = "";
    public DateTime DiscoveredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdated  { get; set; } = DateTime.UtcNow;
    public bool     IsActive     { get; set; } = true;
}
}