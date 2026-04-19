namespace API.DTOs;

public class EbayInventoryResultDto
{
    public int                  Total { get; set; }
    public List<EbayListingDto> Items { get; set; } = [];
}
