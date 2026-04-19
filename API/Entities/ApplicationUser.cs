using Microsoft.AspNetCore.Identity;

namespace API.Entities;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public EbayConnection? EbayConnection { get; set; }
    public AmazonConnection? AmazonConnection { get; set; }
    public ICollection<EbayListing> EbayListings { get; set; } = new List<EbayListing>();
    public ICollection<ProductCache> ProductCache { get; set; } = new List<ProductCache>();
    public ICollection<PriceComparison> PriceComparisons { get; set; } = new List<PriceComparison>();
    public ICollection<DealScan> DealScans { get; set; } = new List<DealScan>();
}