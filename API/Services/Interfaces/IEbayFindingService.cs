namespace API.Services.Interfaces;

public record EbaySoldResult(decimal AveragePrice, int SoldCount);

public interface IEbayFindingService
{
    Task<EbaySoldResult> GetCompletedSoldPricesAsync(string keyword, CancellationToken ct = default);
}