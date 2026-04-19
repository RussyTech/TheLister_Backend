using API.DTOs;

namespace API.Services.Interfaces;

public interface IEbayDashboardService
{
    Task<EbayDashboardOverviewDto> GetOverviewAsync(string userId);
    Task<EbaySalesChartDto> GetSalesChartAsync(string userId, int days);
    Task<EbayFeedbackSummaryDto> GetFeedbackAsync(string userId);
}