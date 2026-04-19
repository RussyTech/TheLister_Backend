using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.DTOs
{
    public class ListingDetailDto
    {
        public int Id { get; set; }
        public string? EbayListingId { get; set; }
        public int? ProductCacheId { get; set; }
        public string? Asin { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal SellingPrice { get; set; }
        public int Quantity { get; set; }
        public string? EbayCategoryId { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public Dictionary<string, string> ItemSpecifics { get; set; } = new();
        public string Condition { get; set; } = string.Empty;
        public string ListingDuration { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? EbayUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? EndedAt { get; set; }
    }
}