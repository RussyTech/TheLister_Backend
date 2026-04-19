namespace API.DTOs;

public class CreateListingDto
{
    // Product basics
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public string Condition { get; set; } = "USED_EXCELLENT";
    public string? ConditionDescription { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "GBP";
    public int Quantity { get; set; } = 1;
    public List<string> ImageUrls { get; set; } = [];
    public string? Brand { get; set; }
    public string? Mpn { get; set; }
    public string? Ean { get; set; }
    public string FulfillmentPolicyId { get; set; } = "";
    public string PaymentPolicyId { get; set; } = "";
    public string ReturnPolicyId { get; set; } = "";
    public string? MerchantLocationKey { get; set; }
    public Dictionary<string, string>? Aspects { get; set; }
}

public class CreateListingResultDto
{
    public bool Success { get; set; }
    public string? ListingId { get; set; }
    public string? EbayUrl { get; set; }
    public string? Error { get; set; }
}

// In EbayListingDto.cs (or wherever EbayPolicyDto lives)
public class EbayPolicyDto
{
    public string PolicyId { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
}

public class EbayPoliciesDto
{
    public List<EbayPolicyDto> FulfillmentPolicies { get; set; } = [];
    public List<EbayPolicyDto> PaymentPolicies { get; set; } = [];
    public List<EbayPolicyDto> ReturnPolicies { get; set; } = [];
}