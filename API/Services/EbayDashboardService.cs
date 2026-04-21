using System.Net.Http.Headers;
using System.Text.Json;
using API.DTOs;
using API.Services.Interfaces;

namespace API.Services;

public class EbayDashboardService : IEbayDashboardService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly IEbayAuthService _ebayAuth;
    private readonly ICacheService _cache;

    private bool IsSandbox => _config["EbaySettings:Environment"] == "sandbox";
    private string ApiBase => IsSandbox ? "https://api.sandbox.ebay.com" : "https://api.ebay.com";
    private string Currency => _config["EbaySettings:Currency"] ?? "GBP";

    public EbayDashboardService(IConfiguration config, IHttpClientFactory http, IEbayAuthService ebayAuth, ICacheService cache)
    {
        _config = config;
        _http = http;
        _ebayAuth = ebayAuth;
        _cache    = cache;
    }

    // ── Overview ──────────────────────────────────────────────────────────

    public async Task<EbayDashboardOverviewDto> GetOverviewAsync(string userId)
    {
        var sales = await GetSalesChartAsync(userId, 90);
        var feedback = await GetFeedbackAsync(userId);

        return new EbayDashboardOverviewDto
        {
            FeedbackPercent = feedback.Percent,
            FeedbackScore = feedback.TotalScore,
            SalesLast90d = sales.Last90Days,
            OrdersLast90d = await GetOrderCountAsync(userId, 90),
            Currency = Currency,
        };
    }

    // ── Sales chart ───────────────────────────────────────────────────────

    public async Task<EbaySalesChartDto> GetSalesChartAsync(string userId, int days)
    {
        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null) return new EbaySalesChartDto { Currency = Currency };

        var client = MakeClient(token);
        var nowUtc = await GetEbayServerTimeAsync(client);

        var from = nowUtc.AddDays(-days).ToString("yyyy-MM-ddT00:00:00.000Z");
        var to = nowUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var url = $"{ApiBase}/sell/fulfillment/v1/order" +
                   $"?filter=creationdate%3A%5B{Uri.EscapeDataString(from)}..{Uri.EscapeDataString(to)}%5D" +
                   $"&limit=200";

        var byDay = new Dictionary<string, decimal>();
        for (var i = days - 1; i >= 0; i--)
            byDay[nowUtc.AddDays(-i).ToString("dd MMM")] = 0m;

        try
        {
            var resp = await client.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[Dashboard Sales] {(int)resp.StatusCode}: {text[..Math.Min(500, text.Length)]}");

            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("orders", out var orders))
                {
                    Console.WriteLine($"[Dashboard Sales] order count: {orders.GetArrayLength()}");
                    foreach (var order in orders.EnumerateArray())
                    {
                        if (!order.TryGetProperty("creationDate", out var dateEl)) continue;
                        if (!order.TryGetProperty("pricingSummary", out var pricing)) continue;
                        if (!pricing.TryGetProperty("total", out var total)) continue;
                        if (!total.TryGetProperty("value", out var val)) continue;

                        if (!decimal.TryParse(val.GetString(),
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var amount)) continue;
                        if (!DateTime.TryParse(dateEl.GetString(), out var date)) continue;

                        var key = date.ToString("dd MMM");
                        if (byDay.ContainsKey(key)) byDay[key] += amount;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dashboard Sales] Error: {ex.Message}");
        }

        var daily = byDay.Select(kv => new EbaySalesDayDto { Date = kv.Key, Amount = kv.Value }).ToList();

        return new EbaySalesChartDto
        {
            Daily = daily,
            Today = byDay.GetValueOrDefault(nowUtc.ToString("dd MMM")),
            Last7Days = daily.TakeLast(7).Sum(d => d.Amount),
            Last31Days = daily.TakeLast(31).Sum(d => d.Amount),
            Last90Days = daily.Sum(d => d.Amount),
            Currency = Currency,
        };
    }

    // ── Feedback ──────────────────────────────────────────────────────────

    public async Task<EbayFeedbackSummaryDto> GetFeedbackAsync(string userId, int limit = 200, int offset = 0)
    {
        var cacheKey = $"dashboard:feedback:{userId}";

        var cached = await _cache.GetAsync<EbayFeedbackSummaryDto>(cacheKey);
        if (cached is not null)
        {
            Console.WriteLine($"[Feedback] Cache HIT — {cached.Recent?.Count ?? 0} entries");
            return cached;
        }

        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null) return new EbayFeedbackSummaryDto();

        var result = new EbayFeedbackSummaryDto();
        var tradingUrl = IsSandbox
            ? "https://api.sandbox.ebay.com/ws/api.dll"
            : "https://api.ebay.com/ws/api.dll";

        var xml = $"""
        <?xml version="1.0" encoding="utf-8"?>
        <GetFeedbackRequest xmlns="urn:ebay:apis:eBLBaseComponents">
            <RequesterCredentials>
                <eBayAuthToken>{token}</eBayAuthToken>
            </RequesterCredentials>
            <FeedbackType>FeedbackReceivedAsSeller</FeedbackType>
            <DetailLevel>ReturnAll</DetailLevel>
            <Pagination>
                <EntriesPerPage>200</EntriesPerPage>
                <PageNumber>1</PageNumber>
            </Pagination>
        </GetFeedbackRequest>
        """;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, tradingUrl);
            req.Content = new StringContent(xml, System.Text.Encoding.UTF8, "text/xml");
            req.Headers.Add("X-EBAY-API-COMPATIBILITY-LEVEL", "967");
            req.Headers.Add("X-EBAY-API-CALL-NAME", "GetFeedback");
            req.Headers.Add("X-EBAY-API-SITEID", "3");
            req.Headers.Add("X-EBAY-API-APP-NAME", _config["EbaySettings:ClientId"] ?? "");
            req.Headers.Add("X-EBAY-API-DEV-NAME", _config["EbaySettings:DevId"] ?? "");
            req.Headers.Add("X-EBAY-API-CERT-NAME", _config["EbaySettings:ClientSecret"] ?? "");

            var client = _http.CreateClient();
            var resp = await client.SendAsync(req);
            var text = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode) return result;

            var doc = System.Xml.Linq.XDocument.Parse(text);
            System.Xml.Linq.XNamespace ns = "urn:ebay:apis:eBLBaseComponents";

            if (int.TryParse(doc.Descendants(ns + "FeedbackScore").FirstOrDefault()?.Value, out var score))
                result.TotalScore = score;

            var summary = doc.Descendants(ns + "FeedbackSummary").FirstOrDefault();
            if (summary is not null)
            {
                if (int.TryParse(summary.Element(ns + "UniquePositiveFeedbackCount")?.Value, out var pos)) result.PositiveLast30d = pos;
                if (int.TryParse(summary.Element(ns + "UniqueNegativeFeedbackCount")?.Value, out var neg)) result.NegativeLast30d = neg;
                if (int.TryParse(summary.Element(ns + "UniqueNeutralFeedbackCount")?.Value, out var neu)) result.NeutralLast30d = neu;

                var total = result.PositiveLast30d + result.NegativeLast30d + result.NeutralLast30d;
                result.Percent = total > 0 ? Math.Round((double)result.PositiveLast30d / total * 100, 1) : 0;
            }

            if (int.TryParse(doc.Descendants(ns + "TotalNumberOfEntries").FirstOrDefault()?.Value, out var totalEntries))
                result.Total = totalEntries;
            else
                result.Total = result.TotalScore;

            result.Recent = doc.Descendants(ns + "FeedbackDetail").Select(d => new EbayFeedbackEntryDto
            {
                Type = (d.Element(ns + "CommentType")?.Value ?? "").ToUpperInvariant(),
                Comment = d.Element(ns + "CommentText")?.Value ?? "",
                BuyerUserId = d.Element(ns + "CommentingUser")?.Value ?? "",
                Date = DateTime.TryParse(d.Element(ns + "CommentTime")?.Value, out var dt)
                                  ? dt.ToString("dd MMM yyyy") : "",
            }).ToList();

            Console.WriteLine($"[Feedback] Score={result.TotalScore} Entries={result.Recent.Count} — caching 30 min");
            await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(30));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Feedback] Error: {ex.Message}");
        }

        return result;
    }

    // ── Order count ───────────────────────────────────────────────────────

    private async Task<int> GetOrderCountAsync(string userId, int days)
    {
        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null) return 0;

        var client = MakeClient(token);
        var nowUtc = await GetEbayServerTimeAsync(client);

        var from = nowUtc.AddDays(-days).ToString("yyyy-MM-ddT00:00:00.000Z");
        var to = nowUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var url = $"{ApiBase}/sell/fulfillment/v1/order" +
                   $"?filter=creationdate%3A%5B{Uri.EscapeDataString(from)}..{Uri.EscapeDataString(to)}%5D" +
                   $"&limit=1";

        try
        {
            var resp = await client.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[Dashboard Orders] {(int)resp.StatusCode}: {text[..Math.Min(200, text.Length)]}");
            if (!resp.IsSuccessStatusCode) return 0;

            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty("total", out var total) ? total.GetInt32() : 0;
        }
        catch { return 0; }
    }

    // ── Server time ───────────────────────────────────────────────────────

    private async Task<DateTime> GetEbayServerTimeAsync(HttpClient client)
    {
        try
        {
            var resp = await client.GetAsync($"{ApiBase}/sell/fulfillment/v1/order?limit=1");
            if (resp.Headers.Date.HasValue)
            {
                var serverTime = resp.Headers.Date.Value.UtcDateTime;
                Console.WriteLine($"[Dashboard] eBay server time: {serverTime:O}  Machine: {DateTime.UtcNow:O}");
                return serverTime;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dashboard] Could not read server time: {ex.Message}");
        }
        return DateTime.UtcNow;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private HttpClient MakeClient(string token)
    {
        var client = _http.CreateClient();
        client.DefaultRequestVersion = new Version(1, 1);
        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-EBAY-C-MARKETPLACE-ID", "EBAY_GB");
        return client;
    }
}