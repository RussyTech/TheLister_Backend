// DTOs/DealFilterParams.cs
namespace API.DTOs;

public class DealFilterParams
{
    public int Page       { get; set; } = 1;
    public int PageSize   { get; set; } = 50;
    public string? Category   { get; set; }
    public decimal? MinRoi    { get; set; }
    public decimal? MinProfit { get; set; }
    public string? SellerType { get; set; }
    public string SortBy     { get; set; } = "DiscoveredAt";
    public bool SortDesc     { get; set; } = true;
}


public class ScanStatusDto
{
    public int TotalDeals     { get; set; }
    public int ActiveDeals    { get; set; }
    public DateTime? LastScan { get; set; }
    public bool IsScanning    { get; set; }
}