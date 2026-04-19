namespace API.DTOs;

public class ProductSearchResult
{
    public List<ProductDto> Products { get; set; } = [];
    public int TotalResults { get; set; }
    public int Page { get; set; }
    public string Query { get; set; } = string.Empty;
}