namespace API.DTOs;

public class EbayCompareDto
{
    public string SearchTitle                   { get; set; } = "";
    public List<EbayMarketListingDto> Active    { get; set; } = [];
    public List<EbaySoldDto>          Sold      { get; set; } = [];
}

public class EbayMarketListingDto
{
    public string   Title       { get; set; } = "";
    public string   Url         { get; set; } = "";
    public string?  Image       { get; set; }
    public decimal? Price       { get; set; }
    public string   Currency    { get; set; } = "GBP";
    public string?  Condition   { get; set; }
    public string?  SellerName  { get; set; }
    public double?  SellerScore { get; set; }
    public bool     IsAuction   { get; set; }
}

public class EbaySoldDto
{
    public decimal Price    { get; set; }
    public string  Currency { get; set; } = "GBP";
    public string? Date     { get; set; }
    public string? Title    { get; set; }
}