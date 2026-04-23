using System.Text.Json;
using API.Services.Interfaces;

namespace API.Services;

public class EbayFindingService(
    HttpClient http,
    IConfiguration config,
    ILogger<EbayFindingService> log) : IEbayFindingService
{
    private const string FindingApiUrl = "https://svcs.ebay.com/services/search/FindingService/v1";

    public async Task<EbaySoldResult> GetCompletedSoldPricesAsync(
        string keyword, CancellationToken ct = default)
    {
        var appId = config["EbaySettings:AppId"] ?? "";
        if (string.IsNullOrEmpty(appId)) return new EbaySoldResult(0, 0);

        var url = $"{FindingApiUrl}" +
                  $"?OPERATION-NAME=findCompletedItems" +
                  $"&SERVICE-VERSION=1.0.0" +
                  $"&SECURITY-APPNAME={Uri.EscapeDataString(appId)}" +
                  $"&RESPONSE-DATA-FORMAT=JSON" +
                  $"&REST-PAYLOAD" +
                  $"&keywords={Uri.EscapeDataString(keyword)}" +
                  $"&Global-ID=EBAY-GB" +
                  $"&itemFilter(0).name=SoldItemsOnly&itemFilter(0).value=true" +
                  $"&itemFilter(1).name=ListingType&itemFilter(1).value=AuctionWithBIN" +
                  $"&itemFilter(2).name=ListingType&itemFilter(2).value=FixedPrice" +
                  $"&sortOrder=EndTimeSoonest" +
                  $"&paginationInput.entriesPerPage=20";

        try
        {
            var json = await http.GetStringAsync(url, ct);
            var doc  = JsonDocument.Parse(json);

            var root = doc.RootElement
                .GetProperty("findCompletedItemsResponse")[0];

            if (!root.TryGetProperty("searchResult", out var results))
                return new EbaySoldResult(0, 0);

            var items = results[0];
            if (!items.TryGetProperty("item", out var itemArr))
                return new EbaySoldResult(0, 0);

            var prices = new List<decimal>();
            foreach (var item in itemArr.EnumerateArray())
            {
                if (item.TryGetProperty("sellingStatus", out var ss) &&
                    ss[0].TryGetProperty("currentPrice", out var cp) &&
                    cp[0].TryGetProperty("__value__", out var val) &&
                    decimal.TryParse(val.GetString(), out var price))
                {
                    prices.Add(price);
                }
            }

            if (prices.Count == 0) return new EbaySoldResult(0, 0);

            var avg = Math.Round(prices.Average(), 2);
            return new EbaySoldResult(avg, prices.Count);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "eBay Finding API error for keyword: {Keyword}", keyword);
            return new EbaySoldResult(0, 0);
        }
    }
}