namespace API.DTOs;

public class PriceComparisonDto
{
    public int Id { get; set; }
    public string Asin { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public decimal AmazonPrice { get; set; }
    public decimal EbayLowestNewPrice { get; set; }
    public decimal EbayAverageNewPrice { get; set; }
    public decimal EstimatedEbayFees { get; set; }
    public decimal EstimatedProfit { get; set; }
    public decimal ProfitMarginPercent { get; set; }
    public string OpportunityScore { get; set; } = string.Empty;  // High / Medium / Low
    public DateTime SnapshotAt { get; set; }
}