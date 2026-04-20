namespace API.DTOs;

public class AmazonProductDto
{
    public string  ProductUrl    { get; set; } = "";
    public string? Asin          { get; set; }
    public string  Title         { get; set; } = "";
    public string  Brand         { get; set; } = "";
    public string  Description   { get; set; } = "";
    public string  Category      { get; set; } = "";
    public bool    ScrapedOk     { get; set; }
    public string? Error         { get; set; }

    public List<string>      ImageUrls      { get; set; } = [];
    public List<string>      BulletPoints   { get; set; } = [];
    public List<ProductSpec> Specifications { get; set; } = [];

    // Pricing
    public bool     CurrentlyUnavailable { get; set; }
    public decimal? BuyBoxPrice          { get; set; }
    public string   Currency             { get; set; } = "GBP";

    // Like New (used_like_new offers)
    public decimal? LikeNewPriceMin { get; set; }
    public decimal? LikeNewPriceMax { get; set; }
    public int?     LikeNewCount    { get; set; }

    // Used – Very Good offers
    public decimal? VeryGoodPriceMin { get; set; }
    public decimal? VeryGoodPriceMax { get; set; }
    public int?     VeryGoodCount    { get; set; }

    // Other sellers (all conditions, sorted cheapest first, max 5)
    public List<OtherSellerDto> OtherSellers { get; set; } = [];
}

public class ProductSpec
{
    public string Name  { get; set; } = "";
    public string Value { get; set; } = "";
}

public class OtherSellerDto
{
    public string  SellerName { get; set; } = "";
    public string  Condition  { get; set; } = "";
    public decimal Price      { get; set; }
    public string  Currency   { get; set; } = "GBP";
}