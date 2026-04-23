using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using API.Entities.DealFinder;

namespace API.Services.Interfaces
{
    public interface IDealFinderService
{
    Task<DealFinderPagedResult> GetDealsAsync(DealFinderFilter filter, CancellationToken ct = default);
    Task<List<string>> GetCategoriesAsync(CancellationToken ct = default);
    Task<DealScanStatus> GetScanStatusAsync(CancellationToken ct = default);
}
public record DealScanStatus(DateTime? LastScanAt, int TotalDeals, int ActiveDeals, bool IsScanning);
}