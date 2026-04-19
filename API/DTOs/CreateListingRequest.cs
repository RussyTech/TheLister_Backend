using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace API.DTOs
{
    public class CreateListingRequest
    {
        public int? ProductCacheId { get; set; }
        [Required]
        [MaxLength(80)]  // eBay title max is 80 characters
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        [Required]
        [Range(0.01, 99999.99)]
        public decimal SellingPrice { get; set; }
        [Required]
        [Range(1, 9999)]
        public int Quantity { get; set; } = 1;
        // eBay requires a category ID before publishing
        public string? EbayCategoryId { get; set; }
        // Image URLs to include in the listing (eBay accepts up to 12)
        public List<string> ImageUrls { get; set; } = new();
        // eBay item specifics e.g. { "Brand": "Nike", "Size": "10", "Color": "Black" }
        public Dictionary<string, string> ItemSpecifics { get; set; } = new();
        // eBay listing condition e.g. "NEW", "LIKE_NEW", "GOOD"
        [Required]
        public string Condition { get; set; } = "NEW";
        // Optional: how long the listing runs. Defaults to GTC (Good Till Cancelled)
        public string ListingDuration { get; set; } = "GTC";
    }
}