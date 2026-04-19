using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using API.DTOs;
using API.Services.Interfaces;

namespace API.Services;

public class EbayPolicyService : IEbayPolicyService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly IEbayAuthService _ebayAuth;

    private bool IsSandbox => _config["EbaySettings:Environment"] == "sandbox";
    private string ApiBase => IsSandbox ? "https://api.sandbox.ebay.com" : "https://api.ebay.com";
    private string MarketId => _config["EbaySettings:MarketplaceId"] ?? "EBAY_GB";
    private string Currency => _config["EbaySettings:Currency"] ?? "GBP";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public EbayPolicyService(IConfiguration config, IHttpClientFactory http, IEbayAuthService ebayAuth)
    {
        _config = config;
        _http = http;
        _ebayAuth = ebayAuth;
    }

    // ── Fetch all policies ────────────────────────────────────────────────

    public async Task<EbayPoliciesDto> GetPoliciesAsync(string userId)
    {
        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null) return new EbayPoliciesDto();

        var client = MakeClient(token);

        var fulfillment = await FetchPoliciesAsync(client, "fulfillment_policy");
        var payment     = await FetchPoliciesAsync(client, "payment_policy");
        var returns     = await FetchPoliciesAsync(client, "return_policy");

        return new EbayPoliciesDto
        {
            FulfillmentPolicies = fulfillment,
            PaymentPolicies     = payment,
            ReturnPolicies      = returns,
        };
    }

    private async Task<List<EbayPolicyDto>> FetchPoliciesAsync(HttpClient client, string policyType)
    {
        try
        {
            var url  = $"{ApiBase}/sell/account/v1/{policyType}?marketplace_id={MarketId}";
            var resp = await client.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[eBay Policies] GET {policyType} {(int)resp.StatusCode}: {text[..Math.Min(300, text.Length)]}");

            if (!resp.IsSuccessStatusCode) return [];

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var arrayKey = policyType switch
            {
                "fulfillment_policy" => "fulfillmentPolicies",
                "payment_policy"     => "paymentPolicies",
                "return_policy"      => "returnPolicies",
                _                    => policyType
            };

            if (!root.TryGetProperty(arrayKey, out var arr)) return [];

            return arr.EnumerateArray().Select(p => new EbayPolicyDto
            {
                PolicyId =
                    p.TryGetProperty("fulfillmentPolicyId", out var fid) ? fid.GetString()! :
                    p.TryGetProperty("paymentPolicyId",     out var pid) ? pid.GetString()! :
                    p.TryGetProperty("returnPolicyId",      out var rid) ? rid.GetString()! : "",
                Name        = p.TryGetProperty("name",        out var n) ? n.GetString() ?? "" : "",
                Description = p.TryGetProperty("description", out var d) ? d.GetString()       : null,
            }).Where(p => !string.IsNullOrEmpty(p.PolicyId)).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eBay Policies] Error fetching {policyType}: {ex.Message}");
            return [];
        }
    }

    // ── Auto-create default policies ──────────────────────────────────────

    public async Task<EbayPolicySetupResultDto> SetupDefaultPoliciesAsync(string userId)
    {
        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null)
            return new EbayPolicySetupResultDto { Success = false, Error = "eBay account not connected." };

        var client = MakeClient(token);

        var (fulfillmentId, fulfillmentError, needsEnrollment) = await CreateFulfillmentPolicyAsync(client);
        if (needsEnrollment)
            return new EbayPolicySetupResultDto
            {
                Success               = false,
                NeedsManualEnrollment = true,
                Error                 = "Your eBay account needs Business Policies enabled. Please visit eBay Seller Hub to activate it."
            };

        if (fulfillmentId is null)
            return new EbayPolicySetupResultDto { Success = false, Error = $"Shipping policy: {fulfillmentError}" };

        var (returnId, returnError) = await CreateReturnPolicyAsync(client);
        if (returnId is null)
            return new EbayPolicySetupResultDto { Success = false, Error = $"Return policy: {returnError}" };

        Console.WriteLine($"[eBay Policies] Auto-created: fulfillment={fulfillmentId}, return={returnId}");

        return new EbayPolicySetupResultDto
        {
            Success             = true,
            FulfillmentPolicyId = fulfillmentId,
            ReturnPolicyId      = returnId,
        };
    }

    private async Task<(string? id, string? error, bool needsEnrollment)> CreateFulfillmentPolicyAsync(HttpClient client)
    {
        var body = new
        {
            name           = "SyncPilot Standard Shipping",
            marketplaceId  = MarketId,
            categoryTypes  = new[] { new { name = "ALL_EXCLUDING_MOTORS_VEHICLES" } },
            handlingTime   = new { value = 1, unit = "DAY" },
            shippingOptions = new[]
            {
                new
                {
                    costType         = "FLAT_RATE",
                    optionType       = "DOMESTIC",
                    shippingServices = new[]
                    {
                        new
                        {
                            shippingServiceCode         = "UK_RoyalMailSecondClassStandard",
                            buyerResponsibleForShipping = false,
                            freeShipping                = true,
                            shippingCost                = new { value = "0.00", currency = Currency }
                        }
                    }
                }
            }
        };

        var resp = await client.PostAsync(
            $"{ApiBase}/sell/account/v1/fulfillment_policy",
            ToJsonContent(body));
        var text = await resp.Content.ReadAsStringAsync();

        Console.WriteLine($"[eBay Policies] POST fulfillment_policy {(int)resp.StatusCode}: {text[..Math.Min(400, text.Length)]}");

        if (!resp.IsSuccessStatusCode)
        {
            bool notEligible = text.Contains("20403") || text.Contains("not eligible");
            return (null, text[..Math.Min(200, text.Length)], notEligible);
        }

        using var doc = JsonDocument.Parse(text);
        var id = doc.RootElement.TryGetProperty("fulfillmentPolicyId", out var fid) ? fid.GetString() : null;
        return (id, null, false);
    }

    private async Task<(string? id, string? error)> CreateReturnPolicyAsync(HttpClient client)
    {
        var body = new
        {
            name                   = "SyncPilot Standard Returns",
            marketplaceId          = MarketId,
            categoryTypes          = new[] { new { name = "ALL_EXCLUDING_MOTORS_VEHICLES" } },
            returnsAccepted        = true,
            returnPeriod           = new { value = 30, unit = "DAY" },
            returnShippingCostPayer = "SELLER",
            refundMethod           = "MONEY_BACK"
        };

        var resp = await client.PostAsync(
            $"{ApiBase}/sell/account/v1/return_policy",
            ToJsonContent(body));
        var text = await resp.Content.ReadAsStringAsync();

        Console.WriteLine($"[eBay Policies] POST return_policy {(int)resp.StatusCode}: {text[..Math.Min(400, text.Length)]}");

        if (!resp.IsSuccessStatusCode)
            return (null, text[..Math.Min(200, text.Length)]);

        using var doc = JsonDocument.Parse(text);
        var id = doc.RootElement.TryGetProperty("returnPolicyId", out var rid) ? rid.GetString() : null;
        return (id, null);
    }

    // ── Category suggestions ──────────────────────────────────────────────

    public async Task<List<EbayCategorySuggestionDto>> GetCategorySuggestionsAsync(string userId, string query)
    {
        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null) return [];

        var client = MakeClient(token);
        var treeId = _config["EbaySettings:SiteId"] ?? "3";
        var url    = $"{ApiBase}/commerce/taxonomy/v1/category_tree/{treeId}/get_category_suggestions?q={Uri.EscapeDataString(query)}";

        try
        {
            var resp = await client.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[eBay Categories] {(int)resp.StatusCode}: {text[..Math.Min(300, text.Length)]}");

            if (!resp.IsSuccessStatusCode) return [];

            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("categorySuggestions", out var arr)) return [];

            return arr.EnumerateArray()
                .Select(s =>
                {
                    var cat  = s.GetProperty("category");
                    var id   = cat.TryGetProperty("categoryId",   out var cid)   ? cid.GetString()   ?? "" : "";
                    var name = cat.TryGetProperty("categoryName", out var cname) ? cname.GetString() ?? "" : "";

                    var path = name;
                    if (s.TryGetProperty("categoryTreeNodeAncestors", out var anc))
                    {
                        var parts = anc.EnumerateArray()
                            .Select(a => a.TryGetProperty("categoryName", out var an) ? an.GetString() : null)
                            .Where(n => n is not null)
                            .ToList();
                        if (parts.Count > 0)
                            path = string.Join(" › ", parts) + " › " + name;
                    }

                    return new EbayCategorySuggestionDto { CategoryId = id, CategoryName = name, CategoryPath = path };
                })
                .Where(c => !string.IsNullOrEmpty(c.CategoryId))
                .Take(12)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eBay Categories] Error: {ex.Message}");
            return [];
        }
    }

    // ── Category item specifics (aspects) ─────────────────────────────────

    public async Task<List<EbayCategoryAspectDto>> GetCategoryAspectsAsync(string userId, string categoryId)
    {
        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null) return [];

        var client = MakeClient(token);
        var treeId = _config["EbaySettings:SiteId"] ?? "3";
        var url    = $"{ApiBase}/commerce/taxonomy/v1/category_tree/{treeId}/get_item_aspects_for_category?category_id={Uri.EscapeDataString(categoryId)}";

        try
        {
            var resp = await client.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[eBay Aspects] {(int)resp.StatusCode}: {text[..Math.Min(200, text.Length)]}");

            if (!resp.IsSuccessStatusCode) return [];

            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("aspects", out var aspects)) return [];

            return aspects.EnumerateArray().Select(a =>
            {
                var name     = a.TryGetProperty("localizedAspectName", out var n) ? n.GetString() ?? "" : "";
                var required = false;

                if (a.TryGetProperty("aspectConstraint", out var constraint))
                    if (constraint.TryGetProperty("aspectRequired", out var req))
                        required = req.GetBoolean();

                var values = new List<string>();
                if (a.TryGetProperty("aspectValues", out var vals))
                    values = vals.EnumerateArray()
                        .Select(v => v.TryGetProperty("localizedValue", out var lv) ? lv.GetString() ?? "" : "")
                        .Where(v => !string.IsNullOrEmpty(v))
                        .ToList();

                return new EbayCategoryAspectDto { Name = name, Required = required, AllowedValues = values };
            })
            .Where(a => !string.IsNullOrEmpty(a.Name))
            .OrderByDescending(a => a.Required)
            .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eBay Aspects] Error: {ex.Message}");
            return [];
        }
    }

    // ── Category valid conditions ─────────────────────────────────────────

    public async Task<List<EbayCategoryConditionDto>> GetCategoryConditionsAsync(string userId, string categoryId)
    {
        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null) return DefaultConditions();

        var client = MakeClient(token);
        var treeId = _config["EbaySettings:SiteId"] ?? "3";
        var url    = $"{ApiBase}/sell/metadata/v1/marketplace/{MarketId}/get_item_condition_policies" +
                     $"?category_tree_id={treeId}&category_id={Uri.EscapeDataString(categoryId)}";

        try
        {
            var resp = await client.GetAsync(url);
            var text = await resp.Content.ReadAsStringAsync();

            Console.WriteLine($"[eBay Conditions] category={categoryId} {(int)resp.StatusCode}: {text[..Math.Min(500, text.Length)]}");

            if (!resp.IsSuccessStatusCode) return DefaultConditions();

            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("itemConditionPolicies", out var policies)) return DefaultConditions();

            var policy = policies.EnumerateArray().FirstOrDefault();
            if (policy.ValueKind == JsonValueKind.Undefined) return DefaultConditions();
            if (!policy.TryGetProperty("itemConditions", out var conditions)) return DefaultConditions();

            var result = conditions.EnumerateArray().Select(c =>
            {
                var numericId = c.TryGetProperty("conditionId",          out var cid)   ? cid.GetString()   ?? "" : "";
                var desc      = c.TryGetProperty("conditionDescription", out var cdesc) ? cdesc.GetString() ?? "" : "";
                var enumStr   = MapConditionIdToEnum(numericId);

                Console.WriteLine($"[eBay Conditions]   {numericId} → {enumStr} ({desc})");

                return new EbayCategoryConditionDto { ConditionId = enumStr, Description = desc };
            })
            .Where(c => !string.IsNullOrEmpty(c.ConditionId))
            .ToList();

            return result.Count > 0 ? result : DefaultConditions();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eBay Conditions] Error: {ex.Message}");
            return DefaultConditions();
        }
    }

    // Maps eBay numeric condition IDs → Inventory API string enums
    private static string MapConditionIdToEnum(string id) => id switch
    {
        "1000" => "NEW",
        "1500" => "NEW_OTHER",
        "1750" => "NEW_WITH_DEFECTS",
        "2000" => "MANUFACTURER_REFURBISHED",
        "2010" => "CERTIFIED_REFURBISHED",
        "2020" => "EXCELLENT_REFURBISHED",
        "2030" => "VERY_GOOD_REFURBISHED",
        "2040" => "GOOD_REFURBISHED",
        "2500" => "SELLER_REFURBISHED",
        "2750" => "LIKE_NEW",
        "3000" => "USED_EXCELLENT",
        "4000" => "USED_VERY_GOOD",
        "5000" => "USED_GOOD",
        "6000" => "USED_ACCEPTABLE",
        "7000" => "FOR_PARTS_OR_NOT_WORKING",
        _      => ""   // unknown → discard so invalid IDs never reach inventory API
    };

    private static List<EbayCategoryConditionDto> DefaultConditions() =>
    [
        new() { ConditionId = "NEW",                      Description = "New" },
        new() { ConditionId = "LIKE_NEW",                 Description = "Opened – never used" },
        new() { ConditionId = "USED_EXCELLENT",           Description = "Used – Excellent" },
        new() { ConditionId = "USED_GOOD",                Description = "Used – Good" },
        new() { ConditionId = "USED_ACCEPTABLE",          Description = "Used – Acceptable" },
        new() { ConditionId = "FOR_PARTS_OR_NOT_WORKING", Description = "For parts or not working" },
    ];

    // ── Helpers ───────────────────────────────────────────────────────────

    private HttpClient MakeClient(string token)
    {
        var client = _http.CreateClient();
        client.DefaultRequestVersion = new Version(1, 1);
        client.DefaultVersionPolicy  = HttpVersionPolicy.RequestVersionExact;
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static StringContent ToJsonContent(object body) =>
        new(JsonSerializer.Serialize(body, JsonOpts), Encoding.UTF8, "application/json");
}