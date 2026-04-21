namespace API.DTOs;

public class EbayDashboardOverviewDto
{
    public string SellerName { get; set; } = "";
    public double FeedbackPercent { get; set; }
    public int FeedbackScore { get; set; }
    public int ListingViewsLast90d { get; set; }
    public decimal SalesLast90d { get; set; }
    public int OrdersLast90d { get; set; }
    public string Currency { get; set; } = "GBP";
}

public class EbaySalesDayDto
{
    public string Date { get; set; } = "";   // "dd MMM"
    public decimal Amount { get; set; }
}

public class EbaySalesChartDto
{
    public List<EbaySalesDayDto> Daily { get; set; } = [];
    public decimal Today { get; set; }
    public decimal Last7Days { get; set; }
    public decimal Last31Days { get; set; }
    public decimal Last90Days { get; set; }
    public string Currency { get; set; } = "GBP";
}

public class EbayFeedbackEntryDto
{
    public string Type { get; set; } = "";   // "POSITIVE" | "NEGATIVE" | "NEUTRAL"
    public string Comment { get; set; } = "";
    public string BuyerUserId { get; set; } = "";
    public string Date { get; set; } = "";
}

public class EbayFeedbackSummaryDto
{
    public int PositiveLast30d { get; set; }
    public int NeutralLast30d { get; set; }
    public int NegativeLast30d { get; set; }
    public double Percent { get; set; }
    public int TotalScore { get; set; }
    public List<EbayFeedbackEntryDto> Recent { get; set; } = [];
    public int Total { get; set; }
}