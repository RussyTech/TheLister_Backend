namespace API.DTOs;

public class EbayInventoryPageDto
{
    public int Total { get; set; }
    public int Limit { get; set; }
    public int Offset { get; set; }
    public List<EbayListingDto> Items { get; set; } = [];
}

public class EbayListingDto
{
    public string Sku { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string? Condition { get; set; }
    public int Quantity { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string? OfferId { get; set; }
    public string? ListingId { get; set; }
    public string? Status { get; set; }
}
