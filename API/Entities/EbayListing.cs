using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Entities
{
    public class EbayListing
    {
        public int Id { get; set; }
        public string UserId { get; set; }           // FK → ApplicationUser
        public int? ProductCacheId { get; set; }     // FK → ProductCache (nullable if manual)
        public string? EbayListingId { get; set; }   // eBay's listing ID (null if draft)
        public string Title { get; set; }
        public string? Description { get; set; }
        public decimal SellingPrice { get; set; }
        public int Quantity { get; set; }
        public string? EbayCategoryId { get; set; }
        public string? EbayUrl { get; set; }
        public ListingStatus Status { get; set; }    // Draft, Active, Ended, Sold
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? EndedAt { get; set; }

        public ApplicationUser User { get; set; }
        public ProductCache? Product { get; set; }

        public List<string> ImageUrls { get; set; } = new();
        public Dictionary<string, string> ItemSpecifics { get; set; } = new();

        public string Condition { get; set; } = "NEW";
        public string ListingDuration { get; set; } = "GTC";
    }

    public enum ListingStatus { Draft, Active, Ended, Sold }
}