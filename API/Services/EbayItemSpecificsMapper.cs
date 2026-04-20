using API.DTOs;

namespace API.Services;

public static class EbayItemSpecificsMapper
{
    // Amazon spec name → eBay aspect name
    private static readonly Dictionary<string, string> NameMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // ── Identity / Brand ────────────────────────────────────────────
            ["brand"]                          = "Brand",
            ["manufacturer"]                   = "Brand",

            // ── Model / Part numbers ─────────────────────────────────────────
            ["model"]                          = "Model",
            ["model number"]                   = "Model",
            ["item model number"]              = "Model",
            ["model name"]                     = "Model",
            ["part number"]                    = "Manufacturer Part Number",
            ["mpn"]                            = "Manufacturer Part Number",
            ["manufacturer part number"]       = "Manufacturer Part Number",
            ["asin"]                           = "ASIN",

            // ── Barcodes ─────────────────────────────────────────────────────
            ["ean"]                            = "EAN",
            ["upc"]                            = "UPC",
            ["isbn"]                           = "ISBN",
            ["gtin"]                           = "GTIN",

            // ── Physical / Appearance ────────────────────────────────────────
            ["colour"]                         = "Colour",
            ["color"]                          = "Colour",
            ["size"]                           = "Size",
            ["material"]                       = "Material",
            ["item weight"]                    = "Item Weight",
            ["package weight"]                 = "Item Weight",
            ["product dimensions"]             = "Item Dimensions",
            ["item dimensions lwh"]            = "Item Dimensions",
            ["standing screen display size"]   = "Screen Size",
            ["screen size"]                    = "Screen Size",
            ["display size"]                   = "Screen Size",
            ["resolution"]                     = "Display Resolution",
            ["display resolution"]             = "Display Resolution",
            ["maximum resolution"]             = "Display Resolution",

            // ── Electronics / Tech ───────────────────────────────────────────
            ["connectivity technology"]        = "Connectivity",
            ["wireless technology"]            = "Wireless Technology",
            ["wireless type"]                  = "Wireless Technology",
            ["compatible devices"]             = "Compatible Model",
            ["compatible with"]                = "Compatible Model",
            ["operating system"]               = "Operating System",
            ["processor brand"]                = "Processor",
            ["processor type"]                 = "Processor",
            ["processor"]                      = "Processor",
            ["processor speed"]                = "Processor Speed",
            ["cpu speed"]                      = "Processor Speed",
            ["ram"]                            = "RAM",
            ["ram memory installed size"]      = "RAM",
            ["memory storage capacity"]        = "Storage Capacity",
            ["hard disk size"]                 = "Storage Capacity",
            ["storage capacity"]               = "Storage Capacity",
            ["flash memory size"]              = "Storage Capacity",
            ["graphics card ram size"]         = "GPU RAM",
            ["graphics coprocessor"]           = "Graphics Processing Unit",
            ["gpu"]                            = "Graphics Processing Unit",
            ["batteries included"]             = "Batteries Included",
            ["batteries required"]             = "Batteries Required",
            ["battery type"]                   = "Battery Type",
            ["number of batteries"]            = "Number of Batteries",
            ["voltage"]                        = "Voltage",
            ["wattage"]                        = "Wattage",
            ["amperage"]                       = "Amperage",

            // ── Camera / Optics ───────────────────────────────────────────────
            ["megapixels"]                     = "Megapixels",
            ["optical zoom"]                   = "Optical Zoom",
            ["digital zoom"]                   = "Digital Zoom",
            ["lens type"]                      = "Lens Type",
            ["lens mount"]                     = "Lens Mount",
            ["image stabilization"]            = "Image Stabilisation",
            ["video capture resolution"]       = "Video Resolution",

            // ── Audio ─────────────────────────────────────────────────────────
            ["speaker maximum output power"]   = "Speaker Output Wattage",
            ["noise reduction technology"]     = "Noise Cancellation",
            ["audio output mode"]              = "Surround Sound Channels",
            ["impedance"]                      = "Impedance",
            ["frequency response"]             = "Frequency Response",
            ["microphone form factor"]         = "Microphone Form Factor",

            // ── Networking ────────────────────────────────────────────────────
            ["wifi standard"]                  = "Wi-Fi Standard",
            ["wi-fi technology"]               = "Wi-Fi Standard",
            ["data transfer rate"]             = "Data Transfer Rate",
            ["ethernet interface"]             = "Network Interface",
            ["lan interface"]                  = "Network Interface",

            // ── Gaming ────────────────────────────────────────────────────────
            ["platform"]                       = "Platform",
            ["esrb rating"]                    = "ESRB Rating",
            ["pegi rating"]                    = "PEGI Rating",
            ["genre"]                          = "Genre",
            ["number of players"]              = "Number of Players",
            ["supported game modes"]           = "Game Mode",

            // ── Clothing / Shoes ─────────────────────────────────────────────
            ["department"]                     = "Department",
            ["style"]                          = "Style",
            ["fit type"]                       = "Fit",
            ["sole material"]                  = "Sole",
            ["outer material"]                 = "Upper Material",
            ["lining material"]                = "Lining",
            ["fabric type"]                    = "Fabric Type",
            ["care instructions"]              = "Care Instructions",
            ["closure type"]                   = "Closure",

            // ── Home / Kitchen / Tools ───────────────────────────────────────
            ["capacity"]                       = "Capacity",
            ["volume"]                         = "Capacity",
            ["number of pieces"]               = "Number of Pieces",
            ["item package quantity"]          = "Quantity",
            ["number of settings"]             = "Number of Settings",
            ["heating element type"]           = "Heating Method",
            ["power source"]                   = "Power Source",
            ["mounting type"]                  = "Mounting Type",
            ["included components"]            = "Included Items",
            ["age range description"]          = "Age Range (Description)",
            ["age range"]                      = "Age Range (Description)",
            ["number of items"]                = "Bundle Description",

            // ── Origin / Legal ───────────────────────────────────────────────
            ["country of origin"]              = "Country/Region of Manufacture",
            ["country/region of origin"]       = "Country/Region of Manufacture",

            // ── Misc ──────────────────────────────────────────────────────────
            ["type"]                           = "Type",
            ["sub-type"]                       = "Sub-Type",
            ["sub type"]                       = "Sub-Type",
            ["theme"]                          = "Theme",
            ["pattern"]                        = "Pattern",
            ["finish type"]                    = "Finish",
            ["shape"]                          = "Shape",
            ["special feature"]                = "Features",
            ["special features"]               = "Features",
            ["item shape"]                     = "Shape",
            ["sport type"]                     = "Sport",
            ["sport"]                          = "Sport",
            ["form factor"]                    = "Form Factor",
            ["hand orientation"]               = "Handedness",
        };

    /// <summary>
    /// Maps a list of Amazon product specifications to a deduplicated list
    /// of eBay ItemSpecific name/value pairs. Unknown spec names are passed
    /// through as-is so eBay can accept any that match its taxonomy.
    /// </summary>
    public static List<ItemSpecific> Map(
        List<ProductSpec> specs,
        string? brand = null)
    {
        var result = new List<ItemSpecific>();
        var seen   = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Ensure Brand always comes first if we have it
        var effectiveBrand = brand?.Trim()
            ?? specs.FirstOrDefault(s =>
                s.Name.Equals("brand", StringComparison.OrdinalIgnoreCase) ||
                s.Name.Equals("manufacturer", StringComparison.OrdinalIgnoreCase))?.Value;

        if (!string.IsNullOrWhiteSpace(effectiveBrand) && seen.Add("Brand"))
        {
            result.Add(new ItemSpecific { Name = "Brand", Value = [effectiveBrand] });
        }

        foreach (var spec in specs)
        {
            if (string.IsNullOrWhiteSpace(spec.Name) || string.IsNullOrWhiteSpace(spec.Value))
                continue;

            var ebayName = NameMap.TryGetValue(spec.Name, out var mapped)
                ? mapped
                : TitleCase(spec.Name); // pass-through with tidy casing

            if (!seen.Add(ebayName)) continue; // deduplicate

            result.Add(new ItemSpecific { Name = ebayName, Value = [spec.Value] });
        }

        return result;
    }

    private static string TitleCase(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        return System.Globalization.CultureInfo.CurrentCulture
            .TextInfo.ToTitleCase(s.ToLowerInvariant());
    }
}

/// <summary>eBay item specific name/value pair used in listing requests.</summary>
public class ItemSpecific
{
    public string        Name  { get; set; } = "";
    public List<string>  Value { get; set; } = [];
}