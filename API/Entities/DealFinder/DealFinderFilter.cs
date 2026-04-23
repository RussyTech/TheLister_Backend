using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Entities.DealFinder
{
   public class DealFinderFilter
{
    public int Page     { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? Keyword      { get; set; }
    public string? Category     { get; set; }
    public string? SellerType   { get; set; }  // Amazon | FBA | FBM
    public decimal? MinBuyPrice   { get; set; }
    public decimal? MaxBuyPrice   { get; set; }
    public decimal? MinSellPrice  { get; set; }
    public decimal? MaxSellPrice  { get; set; }
    public decimal? MinProfit { get; set; }
    public decimal? MaxProfit { get; set; }
    public decimal? MinRoi    { get; set; }
    public decimal? MaxRoi    { get; set; }
    public decimal? MinPriceVariation    { get; set; }
    public decimal? MaxPriceVariation    { get; set; }
    public decimal? MinPriceVariationPct { get; set; }
    public decimal? MaxPriceVariationPct { get; set; }
    public int?     MinSalesRankDrops30 { get; set; }
    public int?     MaxSalesRank        { get; set; }
    public int?     MinSellerCount      { get; set; }
    public int?     MaxSellerCount      { get; set; }
    public int?     MinReviewCount      { get; set; }
    public decimal? MinRating           { get; set; }
    public DateTime? DiscoveredAfter { get; set; }
    public string SortBy   { get; set; } = "DiscoveredAt";
    public bool   SortDesc { get; set; } = true;
}
}