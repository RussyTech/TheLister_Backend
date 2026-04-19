using System.Text;
using System.Xml.Linq;
using API.DTOs;
using API.Services.Interfaces;

namespace API.Services;

public class EbayInventoryService : IEbayInventoryService
{
    private readonly IConfiguration     _config;
    private readonly IHttpClientFactory _http;
    private readonly IEbayAuthService   _ebayAuth;

    private bool   IsSandbox     => _config["EbaySettings:Environment"] == "sandbox";
    private string TradingApiUrl => IsSandbox
        ? "https://api.sandbox.ebay.com/ws/1"
        : "https://api.ebay.com/ws/1";
    private string AppId  => _config["EbaySettings:ClientId"]!;
    private string DevId  => _config["EbaySettings:DevId"]!;
    private string CertId => _config["EbaySettings:ClientSecret"]!;
    private string SiteId => _config["EbaySettings:SiteId"] ?? "3";

    private const string NS = "urn:ebay:apis:eBLBaseComponents";

    public EbayInventoryService(
        IConfiguration config,
        IHttpClientFactory http,
        IEbayAuthService ebayAuth)
    {
        _config   = config;
        _http     = http;
        _ebayAuth = ebayAuth;
    }

    public async Task<EbayInventoryResultDto> GetInventoryAsync(string userId, int limit, int offset)
    {
        var token = await _ebayAuth.GetValidAccessTokenAsync(userId);
        if (token is null)
            return new EbayInventoryResultDto { Total = 0, Items = [] };

        var pageNumber     = (offset / limit) + 1;
        var entriesPerPage = Math.Min(limit, 200);

        var xml    = BuildGetMyeBaySellingXml(token, entriesPerPage, pageNumber);
        var client = _http.CreateClient();

        client.DefaultRequestHeaders.TryAddWithoutValidation("X-EBAY-API-COMPATIBILITY-LEVEL", "1155");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-EBAY-API-DEV-NAME",  DevId);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-EBAY-API-APP-NAME",  AppId);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-EBAY-API-CERT-NAME", CertId);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-EBAY-API-CALL-NAME", "GetMyeBaySelling");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-EBAY-API-SITEID",    SiteId);

        Console.WriteLine($"[eBay Trading] POST {TradingApiUrl} (GetMyeBaySelling)");
        Console.WriteLine($"[eBay Trading] AppId={AppId[..Math.Min(8,AppId.Length)]}... DevId={DevId[..Math.Min(8,DevId.Length)]}...");

        var resp = await client.PostAsync(TradingApiUrl,
            new StringContent(xml, Encoding.UTF8, "text/xml"));
        var body = await resp.Content.ReadAsStringAsync();

        Console.WriteLine($"[eBay Trading] GetMyeBaySelling HTTP {(int)resp.StatusCode}");
        Console.WriteLine($"[eBay Trading] Response (first 1000): {body[..Math.Min(1000, body.Length)]}");

        return ParseInventoryResponse(body);
    }

    // ── XML builder ───────────────────────────────────────────────────────

    private static string BuildGetMyeBaySellingXml(string token, int entriesPerPage, int pageNumber)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(N("GetMyeBaySellingRequest"),
                new XElement(N("RequesterCredentials"),
                    new XElement(N("eBayAuthToken"), token)),
                new XElement(N("ActiveList"),
                    new XElement(N("Include"), "true"),
                    new XElement(N("Pagination"),
                        new XElement(N("EntriesPerPage"), entriesPerPage),
                        new XElement(N("PageNumber"),     pageNumber))),
                new XElement(N("DetailLevel"), "ReturnAll")));

        using var sw = new StringWriter();
        doc.Save(sw);
        return sw.ToString();
    }

    // ── Response parser ───────────────────────────────────────────────────

    private static EbayInventoryResultDto ParseInventoryResponse(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || !body.TrimStart().StartsWith('<'))
        {
            Console.WriteLine($"[eBay Trading] Non-XML response: {body[..Math.Min(200, body.Length)]}");
            return new EbayInventoryResultDto { Total = 0, Items = [] };
        }

        try
        {
            var doc = XDocument.Parse(body);
            var ack = doc.Descendants(N("Ack")).FirstOrDefault()?.Value;

            if (ack is not ("Success" or "Warning"))
            {
                var err = doc.Descendants(N("LongMessage")).FirstOrDefault()?.Value
                       ?? doc.Descendants(N("ShortMessage")).FirstOrDefault()?.Value
                       ?? "Unknown error";
                Console.WriteLine($"[eBay Trading] GetMyeBaySelling error: {err}");
                return new EbayInventoryResultDto { Total = 0, Items = [] };
            }

            var total = int.TryParse(
                doc.Descendants(N("TotalNumberOfEntries")).FirstOrDefault()?.Value, out var t) ? t : 0;

            var items = doc.Descendants(N("Item")).Select(item =>
            {
                var itemId    = item.Element(N("ItemID"))?.Value ?? "";
                var title     = item.Element(N("Title"))?.Value ?? "";
                var priceEl   = item.Descendants(N("CurrentPrice")).FirstOrDefault();
                var price     = decimal.TryParse(priceEl?.Value,
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out var p) ? p : 0m;
                var currency  = priceEl?.Attribute("currencyID")?.Value ?? "GBP";
                var quantity  = int.TryParse(
                    item.Element(N("QuantityAvailable"))?.Value, out var q) ? q : 0;
                var status    = item.Descendants(N("ListingStatus")).FirstOrDefault()?.Value ?? "Active";
                var condition = item.Element(N("ConditionDisplayName"))?.Value
                             ?? MapConditionId(item.Element(N("ConditionID"))?.Value);
                var imageUrl  = item.Descendants(N("PictureURL")).FirstOrDefault()?.Value;

                return new EbayListingDto
                {
                    Sku       = itemId,
                    Title     = title,
                    Price     = price,
                    Currency  = currency,
                    Quantity  = quantity,
                    Status    = status,
                    Condition = condition,
                    ImageUrl  = imageUrl,
                };
            }).ToList();

            return new EbayInventoryResultDto { Total = total, Items = items };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[eBay Trading] ParseInventoryResponse error: {ex.Message}");
            return new EbayInventoryResultDto { Total = 0, Items = [] };
        }
    }

    private static string MapConditionId(string? id) => id switch
    {
        "1000" => "New",
        "1500" => "New – Open box",
        "2000" => "Manufacturer refurbished",
        "2500" => "Seller refurbished",
        "3000" => "Very Good",
        "4000" => "Good",
        "5000" => "Acceptable",
        "7000" => "For parts",
        _      => "Used"
    };

    private static XName N(string local) => XName.Get(local, NS);
}