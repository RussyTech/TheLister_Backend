using System.Globalization;
using API.DTOs;
using API.Services.Interfaces;
using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;

namespace API.Services;

public class SourcingService : ISourcingService
{
    private static readonly Dictionary<string, string> ColumnMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["add?"]               = "status",
        ["add"]                = "status",
        ["status"]             = "status",
        ["title"]              = "title",
        ["product title"]      = "title",
        ["name"]               = "title",
        ["amazon url"]         = "amazonUrl",
        ["amazon"]             = "amazonUrl",
        ["amz url"]            = "amazonUrl",
        ["url amazon"]         = "amazonUrl",   // "URL: Amazon" normalises to this
        ["url: amazon"]        = "amazonUrl",
        ["asin"]               = "asin",
        ["amazon asin"]        = "asin",
        ["ebay url"]           = "ebayUrl",
        ["ebay"]               = "ebayUrl",
        ["buy box"]            = "buyBox",
        ["buybox"]             = "buyBox",
        ["buy box price"]      = "buyBox",
        ["buy box new"]        = "buyBoxNew",
        ["used like new"]      = "likeNew",
        ["like new"]           = "likeNew",
        ["like new price"]     = "likeNew",
        ["likenew"]            = "likeNew",
        ["used good"]          = "usedGood",
        ["used - good"]        = "usedGood",
        ["ebay price"]         = "ebayPrice",
        ["ebay bought"]        = "ebayBought",
        ["bought"]             = "ebayBought",
        ["sales rate"]         = "salesRate",
        ["sell rate"]          = "salesRate",
        ["salesrate"]          = "salesRate",
        ["competitors"]        = "competitors",
        ["competition"]        = "competitors",
        ["margin"]             = "margin",
        ["margin %"]           = "margin",
        ["profit"]             = "profit",
        ["net profit"]         = "profit",
        ["brand"]              = "brand",
        ["category"]           = "category",
        ["ebay selling"]       = "ebaySelling",
        ["ebay sell price"]    = "ebaySelling",
        ["ebay avg"]           = "ebayAvg",
        ["ebay average"]       = "ebayAvg",
        ["notes"]              = "notes",
        ["note"]               = "notes",
    };

    public async Task<SourcingUploadResponseDto> ParseSpreadsheetAsync(IFormFile file)
    {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        return ext switch
        {
            ".csv"             => await ParseCsvAsync(file),
            ".xlsx" or ".xlsm" => await ParseXlsxAsync(file),
            ".xls"             => throw new InvalidOperationException(
                                      "Please save as .xlsx or .csv — legacy .xls is not supported."),
            _                  => throw new InvalidOperationException(
                                      $"Unsupported file type '{ext}'. Upload a .csv or .xlsx file."),
        };
    }

    // ── XLSX ─────────────────────────────────────────────────────────────

    private static Task<SourcingUploadResponseDto> ParseXlsxAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var wb     = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();

        var headerRow = ws.Row(1);
        var headers   = new Dictionary<int, string>();

        foreach (var cell in headerRow.CellsUsed())
        {
            var raw = cell.GetString().Trim();
            var key = Normalise(raw);
            if (ColumnMap.TryGetValue(key, out var field))
                headers[cell.Address.ColumnNumber] = field;
        }

        var response = new SourcingUploadResponseDto
        {
            DetectedColumns = headers.Values.Distinct().ToList()
        };

        int rowIdx = 0;
        foreach (var row in ws.RowsUsed().Skip(1))
        {
            rowIdx++;
            var cells   = row.CellsUsed().ToDictionary(c => c.Address.ColumnNumber, c => c.GetString().Trim());
            var product = MapRow(cells, headers, rowIdx);
            if (string.IsNullOrWhiteSpace(product.Title) && string.IsNullOrWhiteSpace(product.AmazonUrl))
            {
                response.Skipped++;
                continue;
            }
            response.Products.Add(product);
        }

        response.Total  = rowIdx;
        response.Parsed = response.Products.Count;
        return Task.FromResult(response);
    }

    // ── CSV ──────────────────────────────────────────────────────────────

    private static async Task<SourcingUploadResponseDto> ParseCsvAsync(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        using var csv    = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord   = true,
            MissingFieldFound = null,
            BadDataFound      = null,
        });

        await csv.ReadAsync();
        csv.ReadHeader();

        var rawHeaders = csv.HeaderRecord ?? [];
        var headers    = new Dictionary<string, string>();
        foreach (var h in rawHeaders)
        {
            var key = Normalise(h.Trim());
            if (ColumnMap.TryGetValue(key, out var field))
                headers[h] = field;
        }

        var response = new SourcingUploadResponseDto
        {
            DetectedColumns = headers.Values.Distinct().ToList()
        };

        int rowIdx = 0;
        while (await csv.ReadAsync())
        {
            rowIdx++;
            var cells   = rawHeaders.ToDictionary(h => h, h => csv.GetField(h)?.Trim() ?? "");
            var product = MapCsvRow(cells, headers, rowIdx);
            if (string.IsNullOrWhiteSpace(product.Title) && string.IsNullOrWhiteSpace(product.AmazonUrl))
            {
                response.Skipped++;
                continue;
            }
            response.Products.Add(product);
        }

        response.Total  = rowIdx;
        response.Parsed = response.Products.Count;
        return response;
    }

    // ── Mappers ───────────────────────────────────────────────────────────

    private static SourcingProductDto MapRow(
        Dictionary<int, string> cells,
        Dictionary<int, string> headers,
        int rowIdx)
    {
        var mapped = new Dictionary<string, string>();
        foreach (var (col, field) in headers)
            mapped[field] = cells.GetValueOrDefault(col, "");
        return BuildDto(mapped, rowIdx);
    }

    private static SourcingProductDto MapCsvRow(
        Dictionary<string, string> cells,
        Dictionary<string, string> headers,
        int rowIdx)
    {
        var mapped = new Dictionary<string, string>();
        foreach (var (rawHeader, field) in headers)
            mapped[field] = cells.GetValueOrDefault(rawHeader, "");
        return BuildDto(mapped, rowIdx);
    }

    private static SourcingProductDto BuildDto(Dictionary<string, string> m, int rowIdx)
    {
        var dto = new SourcingProductDto
        {
            RowIndex    = rowIdx,
            Status      = m.GetValueOrDefault("status",     ""),
            Title       = m.GetValueOrDefault("title",      ""),
            AmazonUrl   = m.GetValueOrDefault("amazonUrl",  ""),
            EbayUrl     = m.GetValueOrDefault("ebayUrl",    ""),
            BuyBox      = ToDecimal(m.GetValueOrDefault("buyBox")),
            BuyBoxNew   = ToDecimal(m.GetValueOrDefault("buyBoxNew")),
            LikeNew     = ToDecimal(m.GetValueOrDefault("likeNew")),
            UsedGood    = ToDecimal(m.GetValueOrDefault("usedGood")),
            EbayPrice   = ToDecimal(m.GetValueOrDefault("ebayPrice")),
            EbayBought  = ToDecimal(m.GetValueOrDefault("ebayBought")),
            SalesRate   = ToDecimal(m.GetValueOrDefault("salesRate")),
            Competitors = ToInt(m.GetValueOrDefault("competitors")),
            Margin      = ToDecimal(m.GetValueOrDefault("margin")),
            Profit      = ToDecimal(m.GetValueOrDefault("profit")),
            Brand       = m.GetValueOrDefault("brand",      ""),
            Category    = m.GetValueOrDefault("category",   ""),
            EbaySelling = ToDecimal(m.GetValueOrDefault("ebaySelling")),
            EbayAvg     = ToDecimal(m.GetValueOrDefault("ebayAvg")),
            Notes       = m.GetValueOrDefault("notes",      ""),
        };

        // Fallback: build Amazon URL from ASIN if URL column was empty
        if (string.IsNullOrWhiteSpace(dto.AmazonUrl))
        {
            var asin = m.GetValueOrDefault("asin", "").Trim();
            if (!string.IsNullOrWhiteSpace(asin))
                dto.AmazonUrl = $"https://www.amazon.co.uk/dp/{asin}";
        }

        return dto;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string Normalise(string s) =>
        System.Text.RegularExpressions.Regex.Replace(s.ToLowerInvariant(), @"[^a-z0-9 ]", "").Trim();

    private static decimal? ToDecimal(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var cleaned = System.Text.RegularExpressions.Regex.Replace(v, @"[^0-9.\-]", "");
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static int? ToInt(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        var cleaned = System.Text.RegularExpressions.Regex.Replace(v, @"[^0-9\-]", "");
        return int.TryParse(cleaned, out var i) ? i : null;
    }
}