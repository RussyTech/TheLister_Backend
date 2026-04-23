using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Entities.DealFinder
{
   public class DealFinderPagedResult
{
    public int               Total   { get; set; }
    public int               Page    { get; set; }
    public int               Pages   { get; set; }
    public List<DealFinderDealDto> Items { get; set; } = [];
}
public class DealFinderDealDto
{
    public int     Id                     { get; set; }
    public string  Asin                   { get; set; } = "";
    public string  Title                  { get; set; } = "";
    public string? ImageUrl               { get; set; }
    public string? Category               { get; set; }
    public string? Brand                  { get; set; }
    public decimal AmazonLikeNewPrice     { get; set; }
    public decimal AmazonNewPrice         { get; set; }
    public decimal EbayAvgSoldPrice       { get; set; }
    public decimal Profit                 { get; set; }
    public decimal Roi                    { get; set; }
    public int     SalesRank              { get; set; }
    public int     SalesRankDrops30       { get; set; }
    public int     BoughtLastMonth        { get; set; }
    public decimal Rating                 { get; set; }
    public int     ReviewCount            { get; set; }
    public string  SellerType             { get; set; } = "";
    public int     SellerCount            { get; set; }
    public decimal PriceVariation90To30   { get; set; }
    public decimal PriceVariationPct90To30 { get; set; }
    public string  AmazonUrl              { get; set; } = "";
    public DateTime DiscoveredAt          { get; set; }
    public DateTime LastUpdated           { get; set; }
}
}