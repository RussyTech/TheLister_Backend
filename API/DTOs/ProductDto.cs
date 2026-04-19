namespace API.DTOs;

public class ProductDto
{
    public int Id { get; set; }
    public string Asin { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BulletPoints { get; set; }   // JSON array string
    public string? Brand { get; set; }
    public decimal AmazonPrice { get; set; }
    public string? AmazonUrl { get; set; }
    public string? Category { get; set; }
    public decimal? WeightKg { get; set; }
    public DateTime LastFetchedAt { get; set; }
    public List<ProductImageDto> Images { get; set; } = [];

    // Populated from Rainforest search results only (null when from cache)
    public decimal? Rating { get; set; }
    public int? ReviewCount { get; set; }
    public bool IsPrime { get; set; }
}