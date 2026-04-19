using System.Net.Http.Headers;
using System.Text.Json;
using API.DTOs;
using API.Services.Interfaces;

namespace API.Services;

public class EbayDashboardService : IEbayDashboardService
{
    private readonly IConfiguration     _config;
    private readonly IHttpClientFactory _http;
    private readonly IEbayAuthService   _ebayAuth;

    private bool   IsSandbox => _config["EbaySettings:Environment"] == "sandbox";
    private string ApiBase   => IsSandbox ? "https://api.sandbox.ebay.com" : "https://api.ebay.com";
    private string Currency  => _config["EbaySettings:Currency"] ?? "GBP";

    public EbayDashboardService(IConfiguration config, IHttpClientFactory http, IEbayAuthService ebayAuth)
    {
        _config   = config;
        _http     = http;
        _ebayAuth = ebayAuth;
    }

    // ── Overview ──────────────────────────────────────────────────────────

    public async Task<EbayDashboardOverviewDto> GetOverviewAsync(string userId)
    {
        var sales    = await GetSalesChartAsync(userId, 90);
        var feedback = await GetFeedbackAsync(userId);

        return new EbayDashboardOverviewDto
        {
            FeedbackPercent = feedback.Percent,
            FeedbackScore   = feedback.TotalScore,
            SalesLast90d    = sales.Last90Days,
            OrdersLast90d   = await GetOrderCountAsync(userId, 90),
            Currency        = Currency,
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
        var to   = nowUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var url  = $"{ApiBase}/sell/fulfillment/v1/order" +
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
                        if (!order.TryGetProperty("creationDate",   out var dateEl))  continue;
                        if (!order.TryGetProperty("pricingSummary", out var pricing)) continue;
                        if (!pricing.TryGetProperty("total",        out var total))   continue;
                        if (!total.TryGetProperty("value",          out var val))     continue;

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
            Daily      = daily,
            Today      = byDay.GetValueOrDefault(nowUtc.ToString("dd MMM")),
            Last7Days  = daily.TakeLast(7).Sum(d => d.Amount),
            Last31Days = daily.TakeLast(31).Sum(d => d.Amount),
            Last90Days = daily.Sum(d => d.Amount),
            Currency   = Currency,
        };
    }

    // ── Feedback ──────────────────────────────────────────────────────────

    public async Task<EbayFeedbackSummaryDto> GetFeedbackAsync(string userId)
    {
        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null) return new EbayFeedbackSummaryDto();

        var client = MakeClient(token);
        var result = new EbayFeedbackSummaryDto();

        try
        {
            var resp = await client.GetAsync($"{ApiBase}/sell/feedback/v1/feedback_summary");
            var text = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[Dashboard Feedback] summary {(int)resp.StatusCode}: {text[..Math.Min(400, text.Length)]}");

            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                if (root.TryGetProperty("positiveFeedbackPercent", out var pct))
                    result.Percent = pct.GetDouble();

                if (root.TryGetProperty("feedbackLeftCount", out var score))
                    result.TotalScore = score.GetInt32();

                if (root.TryGetProperty("recentFeedbackPeriod", out var period))
                {
                    if (period.TryGetProperty("positiveFeedbackCount", out var pos)) result.PositiveLast30d = pos.GetInt32();
                    if (period.TryGetProperty("neutralFeedbackCount",  out var neu)) result.NeutralLast30d  = neu.GetInt32();
                    if (period.TryGetProperty("negativeFeedbackCount", out var neg)) result.NegativeLast30d = neg.GetInt32();
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[Dashboard Feedback] summary error: {ex.Message}"); }

        try
        {
            var resp = await client.GetAsync($"{ApiBase}/sell/feedback/v1/feedback?limit=5&feedback_type=RECEIVED_AS_SELLER");
            var text = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.TryGetProperty("feedbackList", out var list))
                {
                    result.Recent = list.EnumerateArray().Select(f => new EbayFeedbackEntryDto
                    {
                        Type        = f.TryGetProperty("feedbackType",     out var t) ? t.GetString() ?? "" : "",
                        Comment     = f.TryGetProperty("comment",          out var c) ? c.GetString() ?? "" : "",
                        BuyerUserId = f.TryGetProperty("givingUserSeller", out var u) ? u.GetString() ?? "" : "",
                        Date        = f.TryGetProperty("creationDate",     out var d) &&
                                      DateTime.TryParse(d.GetString(), out var dt)
                                          ? dt.ToString("dd MMM yyyy") : "",
                    }).ToList();
                }
            }
        }
        catch (Exception ex) { Console.WriteLine($"[Dashboard Feedback] entries error: {ex.Message}"); }

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
        var to   = nowUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var url  = $"{ApiBase}/sell/fulfillment/v1/order" +
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
                Console.WriteLine($"[Dashboard] eBay server time: {serverTime:O}  Machine time: {DateTime.UtcNow:O}");
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
        client.DefaultVersionPolicy  = HttpVersionPolicy.RequestVersionExact;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-EBAY-C-MARKETPLACE-ID", "EBAY_GB");
        return client;
    }
}