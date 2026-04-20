using System.ComponentModel.DataAnnotations;
using API.DTOs;

namespace API.DTOs
{
    public class CreateListingRequest
    {
        public int? ProductCacheId { get; set; }

        [Required]
        [MaxLength(80)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [Range(0.01, 99999.99)]
        public decimal SellingPrice { get; set; }

        [Required]
        [Range(1, 9999)]
        public int Quantity { get; set; } = 1;

        public string? EbayCategoryId { get; set; }

        // Image URLs to include in the listing (eBay accepts up to 12)
        public List<string> ImageUrls { get; set; } = new();

        // Raw Amazon/Rainforest specs — backend maps these to eBay ItemSpecifics automatically
        public List<ProductSpec> Specifications { get; set; } = new();

        // Manual overrides — merged on top of auto-mapped specs (last write wins)
        public Dictionary<string, string> ItemSpecifics { get; set; } = new();

        [Required]
        public string Condition { get; set; } = "USED_EXCELLENT";

        public string ListingDuration { get; set; } = "GTC";
    }
}