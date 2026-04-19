namespace API.DTOs;

public class AmazonProductDto
{
    public string ProductUrl { get; set; } = "";
    public string? Asin { get; set; }
    public string Title { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public bool ScrapedOk { get; set; }
    public string? Error { get; set; }

    public List<string> ImageUrls { get; set; } = [];
    public List<string> BulletPoints { get; set; } = [];

    // ── Pricing ──────────────────────────────────────
    public decimal? BuyBoxPrice { get; set; }   // Amazon new Buy Box (live)
    public decimal? LikeNewPriceMin { get; set; }   // cheapest Like New offer
    public decimal? LikeNewPriceMax { get; set; }   // most expensive Like New offer
    public int? LikeNewCount { get; set; }   // number of Like New sellers
    public string Currency { get; set; } = "GBP";
}