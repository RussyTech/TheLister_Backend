namespace API.DTOs;

public class DealScanDto
{
    public int Id { get; set; }
    public string Asin { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Category { get; set; }
    public decimal AmazonPrice { get; set; }
    public decimal EbayAveragePrice { get; set; }
    public decimal PotentialProfit { get; set; }   // computed in mapping
    public decimal ProfitMarginPercent { get; set; }
    public DateTime ScannedAt { get; set; }
}