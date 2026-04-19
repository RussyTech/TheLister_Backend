namespace API.DTOs;

public class SourcingProductDto
{
    public int RowIndex { get; set; }
    public string Status { get; set; } = "";   // Add? / Maybe / Yes / No
    public string Title { get; set; } = "";
    public string AmazonUrl { get; set; } = "";
    public string EbayUrl { get; set; } = "";
    public decimal? BuyBox { get; set; }         // Amazon Buy Box
    public decimal? BuyBoxNew { get; set; }         // Amazon Buy Box New
    public decimal? LikeNew { get; set; }         // Used Like New — key field
    public decimal? UsedGood { get; set; }
    public decimal? EbayPrice { get; set; }
    public decimal? EbayBought { get; set; }
    public decimal? SalesRate { get; set; }         // %
    public int? Competitors { get; set; }
    public decimal? Margin { get; set; }         // %
    public decimal? Profit { get; set; }
    public string Brand { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal? EbaySelling { get; set; }
    public decimal? EbayAvg { get; set; }
    public string Notes { get; set; } = "";
}

public class SourcingUploadResponseDto
{
    public int Total { get; set; }
    public int Parsed { get; set; }
    public int Skipped { get; set; }
    public List<SourcingProductDto> Products { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public List<string> DetectedColumns { get; set; } = [];
}