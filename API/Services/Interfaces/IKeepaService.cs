using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Services.Interfaces
{
    public interface IKeepaService
    {
        Task<KeepaProductData?> GetProductDataAsync(string asin, CancellationToken ct = default);
    }
    public record KeepaProductData(
        int SalesRank,
        int SalesRankDrops30,
        decimal AvgPrice90Days,    // pence → GBP
        decimal AvgPrice30Days,
        decimal CurrentPrice
    );
}