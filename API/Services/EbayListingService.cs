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

    public EbayListingService(IConfiguration config, IHttpClientFactory http, IEbayAuthService ebayAuth)
    {
        _config = config;
        _http = http;
        _ebayAuth = ebayAuth;
    }

    public async Task<CreateListingResultDto> CreateListingAsync(string userId, CreateListingDto dto)
    {
        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null) return Fail("eBay account not connected");

        var sku = "SP-" + Guid.NewGuid().ToString("N")[..12].ToUpper();

        var locationKey = await EnsureMerchantLocationAsync(token);
        if (locationKey is null)
            return Fail("Could not create or find a merchant location on your eBay account");

        var aspects = BuildAspects(dto);

        var itemError = await UpsertInventoryItemAsync(token, sku, dto, aspects);
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

            var retryError = await UpsertInventoryItemAsync(token, sku, dto, aspects);
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
                $"{ApiBase}/sell/inventory/v1/location/{DefaultLocationKey}",
                token, body));
        var createText = await createResp.Content.ReadAsStringAsync();
        Console.WriteLine($"[eBay] POST location {(int)createResp.StatusCode}: {createText[..Math.Min(300, createText.Length)]}");

        if (createResp.IsSuccessStatusCode || (int)createResp.StatusCode == 204)
        {
            await MakeClient().SendAsync(BuildRequest(
                HttpMethod.Post,
                $"{ApiBase}/sell/inventory/v1/location/{DefaultLocationKey}/enable",
                token));
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
            ["Brand"] = !string.IsNullOrWhiteSpace(dto.Brand) ? [dto.Brand] : ["Does Not Apply"]
        };
        if (!string.IsNullOrWhiteSpace(dto.Mpn)) aspects["MPN"] = [dto.Mpn];

        if (dto.Aspects is not null)
            foreach (var (k, v) in dto.Aspects)
                if (!string.IsNullOrWhiteSpace(v))
                    aspects[k] = [v];

        return aspects;
    }

    // ── Step 1: PUT inventory item ────────────────────────────────────────

    private async Task<string?> UpsertInventoryItemAsync(
        string token, string sku, CreateListingDto dto,
        Dictionary<string, List<string>> aspects)
    {
        var body = new
        {
            availability = new
            {
                shipToLocationAvailability = new { quantity = dto.Quantity }
            },
            condition = dto.Condition,
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
        var request = BuildRequest(HttpMethod.Put, url, token, body);
        var resp = await MakeClient().SendAsync(request);
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

        var request = BuildRequest(HttpMethod.Post, $"{ApiBase}/sell/inventory/v1/offer", token, offerBody);
        var resp = await MakeClient().SendAsync(request);
        var text = await resp.Content.ReadAsStringAsync();

        Console.WriteLine($"[eBay] POST offer {(int)resp.StatusCode}: {text[..Math.Min(600, text.Length)]}");

        if (!resp.IsSuccessStatusCode)
            return (null, $"HTTP {(int)resp.StatusCode} — {text[..Math.Min(300, text.Length)]}");

        using var doc = JsonDocument.Parse(text);
        var offerId = doc.RootElement.GetProperty("offerId").GetString();
        return (offerId, null);
    }

    // ── Step 3: POST publish (with missing-aspect extraction) ─────────────

    private async Task<(string? listingId, string? error, List<string> missingAspects)>
        PublishOfferAsync(string token, string offerId)
    {
        var request = BuildRequest(
            HttpMethod.Post,
            $"{ApiBase}/sell/inventory/v1/offer/{offerId}/publish",
            token);

        var resp = await MakeClient().SendAsync(request);
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

    // ── Parse "X is missing" errors from eBay publish response ───────────

    private static List<string> ExtractMissingAspects(string responseBody)
    {
        var missing = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("errors", out var errors)) return missing;

            var regex = new System.Text.RegularExpressions.Regex(
                @"item specific (.+?) is missing",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (var err in errors.EnumerateArray())
            {
                if (err.TryGetProperty("message", out var msgProp))
                {
                    var msg = msgProp.GetString() ?? "";
                    var match = regex.Match(msg);
                    Console.WriteLine($"[eBay] Aspect regex on: '{msg}' → match={match.Success}");
                    if (match.Success)
                    {
                        var aspect = match.Groups[1].Value.Trim();
                        if (!missing.Contains(aspect))
                        {
                            missing.Add(aspect);
                            Console.WriteLine($"[eBay] Found missing aspect: '{aspect}'");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eBay] ExtractMissingAspects error: {ex.Message}");
        }

        Console.WriteLine($"[eBay] Missing aspects: [{string.Join(", ", missing)}]");
        return missing;
    }

    // Add this helper method to EbayListingService:

private static List<ItemSpecific> BuildItemSpecifics(List<ProductSpec> specs)
{
    // eBay name normalisation map — Amazon spec names → eBay aspect names
    var nameMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["brand"]                   = "Brand",
        ["manufacturer"]            = "Brand",
        ["model number"]            = "Model",
        ["model"]                   = "Model",
        ["item model number"]       = "Model",
        ["part number"]             = "Manufacturer Part Number",
        ["mpn"]                     = "Manufacturer Part Number",
        ["manufacturer part number"]= "Manufacturer Part Number",
        ["colour"]                  = "Colour",
        ["color"]                   = "Colour",
        ["size"]                    = "Size",
        ["material"]                = "Material",
        ["item weight"]             = "Item Weight",
        ["product dimensions"]      = "Product Dimensions",
        ["batteries required"]      = "Batteries Required",
        ["country of origin"]       = "Country/Region of Manufacture",
        ["ean"]                     = "EAN",
        ["upc"]                     = "UPC",
        ["isbn"]                    = "ISBN",
        ["type"]                    = "Type",
        ["sub type"]                = "Sub-Type",
        ["theme"]                   = "Theme",
        ["age range"]               = "Age Range (Description)",
        ["number of pieces"]        = "Number of Pieces",
        ["connectivity technology"] = "Connectivity",
        ["wireless type"]           = "Wireless Technology",
        ["operating system"]        = "Operating System",
        ["processor"]               = "Processor",
        ["ram"]                     = "RAM",
        ["storage capacity"]        = "Storage Capacity",
        ["screen size"]             = "Screen Size",
        ["resolution"]              = "Display Resolution",
        ["compatible devices"]      = "Compatible Model",
    };

    var result = new List<ItemSpecific>();
    var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var spec in specs)
    {
        var ebayName = nameMap.TryGetValue(spec.Name, out var mapped) ? mapped : spec.Name;
        if (seen.Add(ebayName))
        {
            result.Add(new ItemSpecific
            {
                Name  = ebayName,
                Value = [spec.Value]
            });
        }
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
        {
            request.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                Encoding.UTF8,
                "application/json");
        }

        return request;
    }

    private static CreateListingResultDto Fail(string msg)
    {
        Console.WriteLine($"[eBay] FAIL: {msg}");
        return new CreateListingResultDto { Success = false, Error = msg };
    }
}