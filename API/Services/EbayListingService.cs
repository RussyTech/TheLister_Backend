using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using API.DTOs;
using API.Services.Interfaces;

namespace API.Services;

public class EbayListingService : IEbayListingService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _http;
    private readonly IEbayAuthService _ebayAuth;
    private readonly IEbayCategoryService _categoryService;

    private bool IsSandbox => _config["EbaySettings:Environment"] == "sandbox";
    private string ApiBase => IsSandbox ? "https://api.sandbox.ebay.com" : "https://api.ebay.com";
    private string MarketId => _config["EbaySettings:MarketplaceId"] ?? "EBAY_GB";
    private string Country => _config["EbaySettings:Country"] ?? "GB";
    private string PostalCode => _config["EbaySettings:PostalCode"] ?? "SW1A 1AA";

    private const string DefaultLocationKey = "SYNCPILOT_DEFAULT";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Preferred condition order when listing a "Like New" Amazon item on eBay.
    // ResolveConditionAsync walks this list and picks the first one eBay accepts
    // for the specific category — so it always succeeds regardless of category.
    private static readonly string[] ConditionPreference =
    [
        "LIKE_NEW",
        "USED_EXCELLENT",
        "SELLER_REFURBISHED",
        "USED_VERY_GOOD",
        "USED_GOOD",
        "USED_ACCEPTABLE",
        "FOR_PARTS_OR_NOT_WORKING",
    ];

    public EbayListingService(IConfiguration config, IHttpClientFactory http,
        IEbayAuthService ebayAuth, IEbayCategoryService categoryService)
    {
        _config = config;
        _http = http;
        _ebayAuth = ebayAuth;
        _categoryService = categoryService;
    }

    public async Task<CreateListingResultDto> CreateListingAsync(string userId, CreateListingDto dto)
    {
        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null) return Fail("eBay account not connected");

        // Auto-resolve category if not provided
        if (string.IsNullOrWhiteSpace(dto.CategoryId))
        {
            dto.CategoryId = await _categoryService.SuggestCategoryIdAsync(userId, dto.Title);
            Console.WriteLine($"[eBay] Auto-resolved category: {dto.CategoryId ?? "none"}");
        }

        // ── Resolve condition to one eBay actually accepts for this category ──
        var condition = await ResolveConditionAsync(token, dto.CategoryId, dto.Condition);
        Console.WriteLine($"[eBay] Condition: requested='{dto.Condition}' resolved='{condition}'");

        var sku = "SP-" + Guid.NewGuid().ToString("N")[..12].ToUpper();

        var locationKey = await EnsureMerchantLocationAsync(token);
        if (locationKey is null)
            return Fail("Could not create or find a merchant location on your eBay account");

        var aspects = BuildAspects(dto);

        var itemError = await UpsertInventoryItemAsync(token, sku, dto, condition, aspects);
        if (itemError is not null) return Fail($"Inventory item: {itemError}");

        var (offerId, offerError) = await CreateOfferAsync(token, sku, dto, locationKey);
        if (offerId is null) return Fail($"Create offer: {offerError}");

        // First publish attempt
        var (listingId, publishError, missingAspects) = await PublishOfferAsync(token, offerId);

        // Auto-retry: if eBay reports missing item specifics, add them and re-publish
        if (listingId is null && missingAspects.Count > 0)
        {
            Console.WriteLine($"[eBay] Auto-adding missing aspects: {string.Join(", ", missingAspects)}");
            foreach (var aspect in missingAspects)
                aspects[aspect] = ["Does Not Apply"];

            var retryError = await UpsertInventoryItemAsync(token, sku, dto, condition, aspects);
            if (retryError is not null) return Fail($"Inventory item retry: {retryError}");

            (listingId, publishError, _) = await PublishOfferAsync(token, offerId);
        }

        if (listingId is null) return Fail($"Publish: {publishError}");

        Console.WriteLine($"[eBay] Listed! ItemID={listingId}");
        return new CreateListingResultDto
        {
            Success = true,
            ListingId = listingId,
            EbayUrl = $"https://www.ebay.co.uk/itm/{listingId}"
        };
    }

    // ── Resolve condition: ask eBay what's valid, pick best match ─────────

    private async Task<string> ResolveConditionAsync(string token, string? categoryId, string desired)
    {
        if (string.IsNullOrWhiteSpace(categoryId))
        {
            Console.WriteLine("[eBay] No categoryId — using condition as-is");
            return desired;
        }

        try
        {
            var url = $"{ApiBase}/sell/metadata/v1/marketplace/{MarketId}/get_listing_conditions" +
                       $"?filter=categoryId:{categoryId}";
            var resp = await MakeClient().SendAsync(BuildRequest(HttpMethod.Get, url, token));
            var text = await resp.Content.ReadAsStringAsync();
            Console.WriteLine($"[eBay] GET listing_conditions {(int)resp.StatusCode}: {text[..Math.Min(500, text.Length)]}");

            if (!resp.IsSuccessStatusCode) return desired;

            // Collect all conditionIds eBay accepts for this category
            using var doc = JsonDocument.Parse(text);

            var validIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (doc.RootElement.TryGetProperty("conditionDescriptions", out var arr))
            {
                foreach (var item in arr.EnumerateArray())
                    if (item.TryGetProperty("conditionId", out var cid))
                        validIds.Add(cid.GetString() ?? "");
            }

            Console.WriteLine($"[eBay] Valid conditions for category {categoryId}: [{string.Join(", ", validIds)}]");

            if (validIds.Count == 0) return desired;

            // If the desired condition is already valid, use it
            if (validIds.Contains(desired)) return desired;

            // Otherwise walk the preference list and pick the first accepted value
            foreach (var candidate in ConditionPreference)
                if (validIds.Contains(candidate))
                {
                    Console.WriteLine($"[eBay] Condition '{desired}' not valid for category — falling back to '{candidate}'");
                    return candidate;
                }

            // Last resort: just take whatever eBay said is valid
            var fallback = validIds.First();
            Console.WriteLine($"[eBay] No preferred condition matched — using first available: '{fallback}'");
            return fallback;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eBay] ResolveCondition error: {ex.Message} — using '{desired}'");
            return desired;
        }
    }

    // ── Ensure merchant location exists ───────────────────────────────────

    private async Task<string?> EnsureMerchantLocationAsync(string token)
    {
        var listResp = await MakeClient().SendAsync(
            BuildRequest(HttpMethod.Get, $"{ApiBase}/sell/inventory/v1/location", token));
        var listText = await listResp.Content.ReadAsStringAsync();
        Console.WriteLine($"[eBay] GET locations {(int)listResp.StatusCode}: {listText[..Math.Min(300, listText.Length)]}");

        if (listResp.IsSuccessStatusCode)
        {
            using var listDoc = JsonDocument.Parse(listText);
            if (listDoc.RootElement.TryGetProperty("locations", out var locs))
            {
                var first = locs.EnumerateArray().FirstOrDefault();
                if (first.ValueKind != JsonValueKind.Undefined &&
                    first.TryGetProperty("merchantLocationKey", out var k))
                {
                    var existing = k.GetString();
                    Console.WriteLine($"[eBay] Using existing location: {existing}");
                    return existing;
                }
            }
        }

        var body = new
        {
            location = new
            {
                address = new { postalCode = PostalCode, country = Country }
            },
            locationTypes = new[] { "WAREHOUSE" },
            merchantLocationStatus = "ENABLED",
            name = "SyncPilot Default Location"
        };

        var createResp = await MakeClient().SendAsync(
            BuildRequest(HttpMethod.Post,
                $"{ApiBase}/sell/inventory/v1/location/{DefaultLocationKey}", token, body));
        var createText = await createResp.Content.ReadAsStringAsync();
        Console.WriteLine($"[eBay] POST location {(int)createResp.StatusCode}: {createText[..Math.Min(300, createText.Length)]}");

        if (createResp.IsSuccessStatusCode || (int)createResp.StatusCode == 204)
        {
            await MakeClient().SendAsync(BuildRequest(
                HttpMethod.Post,
                $"{ApiBase}/sell/inventory/v1/location/{DefaultLocationKey}/enable", token));
            Console.WriteLine($"[eBay] Created + enabled location: {DefaultLocationKey}");
            return DefaultLocationKey;
        }

        if ((int)createResp.StatusCode == 409)
        {
            Console.WriteLine($"[eBay] Location already exists: {DefaultLocationKey}");
            return DefaultLocationKey;
        }

        Console.WriteLine($"[eBay] FAILED to create location: {createText}");
        return null;
    }

    // ── Build aspects dictionary ──────────────────────────────────────────

    private static Dictionary<string, List<string>> BuildAspects(CreateListingDto dto)
    {
        var aspects = new Dictionary<string, List<string>>
        {
            ["Brand"] = !string.IsNullOrWhiteSpace(dto.Brand) ? [dto.Brand] : ["Does Not Apply"],
            ["Model"] = ["Does Not Apply"]
        };

        if (!string.IsNullOrWhiteSpace(dto.Mpn)) aspects["MPN"] = [dto.Mpn];

        if (dto.Specifications is not null)
        {
            var mapped = BuildItemSpecifics(
                dto.Specifications.Select(s => new ProductSpec { Name = s.Name, Value = s.Value }).ToList());
            foreach (var item in mapped)
                if (item.Value.Count > 0 && !string.IsNullOrWhiteSpace(item.Value[0]))
                    aspects[item.Name] = item.Value;
        }

        if (dto.Aspects is not null)
            foreach (var (k, v) in dto.Aspects)
                if (!string.IsNullOrWhiteSpace(v))
                    aspects[k] = [v];

        return aspects;
    }

    // ── Step 1: PUT inventory item ────────────────────────────────────────

    private async Task<string?> UpsertInventoryItemAsync(
        string token, string sku, CreateListingDto dto,
        string condition,
        Dictionary<string, List<string>> aspects)
    {
        var body = new
        {
            availability = new
            {
                shipToLocationAvailability = new { quantity = dto.Quantity }
            },
            condition = condition,
            conditionDescription = dto.ConditionDescription,
            product = new
            {
                title = dto.Title.Length > 80 ? dto.Title[..80] : dto.Title,
                description = dto.Description,
                imageUrls = dto.ImageUrls,
                aspects = aspects
            }
        };

        var url = $"{ApiBase}/sell/inventory/v1/inventory_item/{Uri.EscapeDataString(sku)}";
        var resp = await MakeClient().SendAsync(BuildRequest(HttpMethod.Put, url, token, body));
        var text = await resp.Content.ReadAsStringAsync();

        Console.WriteLine($"[eBay] PUT inventory_item {(int)resp.StatusCode}: {text[..Math.Min(400, text.Length)]}");

        return resp.IsSuccessStatusCode ? null
            : $"HTTP {(int)resp.StatusCode} — {text[..Math.Min(300, text.Length)]}";
    }

    // ── Step 2: POST offer ────────────────────────────────────────────────

    private async Task<(string? offerId, string? error)> CreateOfferAsync(
        string token, string sku, CreateListingDto dto, string locationKey)
    {
        var offerBody = new Dictionary<string, object?>
        {
            ["sku"] = sku,
            ["marketplaceId"] = MarketId,
            ["format"] = "FIXED_PRICE",
            ["availableQuantity"] = dto.Quantity,
            ["categoryId"] = dto.CategoryId,
            ["listingDescription"] = dto.Description,
            ["merchantLocationKey"] = locationKey,
            ["listingPolicies"] = new Dictionary<string, object?>
            {
                ["fulfillmentPolicyId"] = dto.FulfillmentPolicyId,
                ["paymentPolicyId"] = dto.PaymentPolicyId,
                ["returnPolicyId"] = dto.ReturnPolicyId,
            },
            ["pricingSummary"] = new
            {
                price = new
                {
                    currency = dto.Currency,
                    value = dto.Price.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                }
            }
        };

        var resp = await MakeClient().SendAsync(
            BuildRequest(HttpMethod.Post, $"{ApiBase}/sell/inventory/v1/offer", token, offerBody));
        var text = await resp.Content.ReadAsStringAsync();

        Console.WriteLine($"[eBay] POST offer {(int)resp.StatusCode}: {text[..Math.Min(600, text.Length)]}");

        if (!resp.IsSuccessStatusCode)
            return (null, $"HTTP {(int)resp.StatusCode} — {text[..Math.Min(300, text.Length)]}");

        using var doc = JsonDocument.Parse(text);
        var offerId = doc.RootElement.GetProperty("offerId").GetString();
        return (offerId, null);
    }

    // ── Step 3: POST publish ──────────────────────────────────────────────

    private async Task<(string? listingId, string? error, List<string> missingAspects)>
        PublishOfferAsync(string token, string offerId)
    {
        var resp = await MakeClient().SendAsync(BuildRequest(
            HttpMethod.Post,
            $"{ApiBase}/sell/inventory/v1/offer/{offerId}/publish", token));
        var text = await resp.Content.ReadAsStringAsync();

        Console.WriteLine($"[eBay] POST publish {(int)resp.StatusCode}: {text[..Math.Min(400, text.Length)]}");

        if (!resp.IsSuccessStatusCode)
        {
            var missing = ExtractMissingAspects(text);
            return (null, $"HTTP {(int)resp.StatusCode} — {text[..Math.Min(300, text.Length)]}", missing);
        }

        using var doc = JsonDocument.Parse(text);
        var listingId = doc.RootElement.GetProperty("listingId").GetString();
        return (listingId, null, []);
    }

    // ── Extract "X is missing" aspect names from eBay error response ──────

    private static List<string> ExtractMissingAspects(string responseBody)
    {
        var missing = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("errors", out var errors)) return missing;

            var regex = new System.Text.RegularExpressions.Regex(
                @"item\s+specific\s+(.+?)\s+is\s+missing",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (var err in errors.EnumerateArray())
            {
                string? matched = null;

                if (err.TryGetProperty("message", out var msgProp))
                {
                    var msg = msgProp.GetString() ?? "";
                    var m = regex.Match(msg);
                    if (m.Success) matched = m.Groups[1].Value.Trim();
                    Console.WriteLine($"[eBay] Aspect check on message: '{msg}' → match={m.Success}");
                }

                if (matched is null && err.TryGetProperty("parameters", out var prms))
                {
                    foreach (var prm in prms.EnumerateArray())
                    {
                        if (prm.TryGetProperty("value", out var val))
                        {
                            var m = regex.Match(val.GetString() ?? "");
                            if (m.Success) { matched = m.Groups[1].Value.Trim(); break; }
                        }
                    }
                }

                if (matched is not null && !missing.Contains(matched))
                {
                    missing.Add(matched);
                    Console.WriteLine($"[eBay] Found missing aspect: '{matched}'");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eBay] ExtractMissingAspects error: {ex.Message}");
        }

        Console.WriteLine($"[eBay] Missing aspects to auto-fill: [{string.Join(", ", missing)}]");
        return missing;
    }

    // ── Blocked Amazon specs (irrelevant on eBay) ─────────────────────────

    private static readonly HashSet<string> _blockedSpecNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "customer reviews", "best sellers rank", "best seller rank",
            "date first available", "asin", "customer rating",
            "ratings", "reviews", "amazon bestseller rank", "product description",
        };

    private static List<ItemSpecific> BuildItemSpecifics(List<ProductSpec> specs)
    {
        var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["brand"] = "Brand",
            ["manufacturer"] = "Brand",
            ["model number"] = "Model",
            ["model"] = "Model",
            ["item model number"] = "Model",
            ["part number"] = "Manufacturer Part Number",
            ["mpn"] = "Manufacturer Part Number",
            ["manufacturer part number"] = "Manufacturer Part Number",
            ["colour"] = "Colour",
            ["color"] = "Colour",
            ["size"] = "Size",
            ["material"] = "Material",
            ["item weight"] = "Item Weight",
            ["product dimensions"] = "Product Dimensions",
            ["batteries required"] = "Batteries Required",
            ["country of origin"] = "Country/Region of Manufacture",
            ["ean"] = "EAN",
            ["upc"] = "UPC",
            ["isbn"] = "ISBN",
            ["type"] = "Type",
            ["sub type"] = "Sub-Type",
            ["theme"] = "Theme",
            ["age range"] = "Age Range (Description)",
            ["number of pieces"] = "Number of Pieces",
            ["connectivity technology"] = "Connectivity",
            ["wireless type"] = "Wireless Technology",
            ["operating system"] = "Operating System",
            ["processor"] = "Processor",
            ["ram"] = "RAM",
            ["storage capacity"] = "Storage Capacity",
            ["screen size"] = "Screen Size",
            ["resolution"] = "Display Resolution",
            ["compatible devices"] = "Compatible Model",
        };

        var result = new List<ItemSpecific>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in specs)
        {
            if (_blockedSpecNames.Contains(spec.Name)) continue;

            var ebayName = nameMap.TryGetValue(spec.Name, out var mapped) ? mapped : spec.Name;
            var value = spec.Value?.Trim() ?? "";
            if (value.Length > 65) value = value[..65].TrimEnd();

            if (!string.IsNullOrWhiteSpace(value) && seen.Add(ebayName))
                result.Add(new ItemSpecific { Name = ebayName, Value = [value] });
        }

        return result;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private HttpClient MakeClient()
    {
        var client = _http.CreateClient();
        client.DefaultRequestVersion = new Version(1, 1);
        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        return client;
    }

    private static HttpRequestMessage BuildRequest(
        HttpMethod method, string url, string token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Version = new Version(1, 1);
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

        if (body is not null)
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                Encoding.UTF8,
                "application/json");

        return request;
    }

    private static CreateListingResultDto Fail(string msg)
    {
        Console.WriteLine($"[eBay] FAIL: {msg}");
        return new CreateListingResultDto { Success = false, Error = msg };
    }
}