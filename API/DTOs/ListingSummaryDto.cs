using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.DTOs
{
    public class ListingSummaryDto
    {
        public int Id { get; set; }
        public string? EbayListingId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal SellingPrice { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = string.Empty;   // Draft, Active, Ended, Sold
        public string? EbayUrl { get; set; }
        public string? PrimaryImageUrl { get; set; }
        public string? Asin { get; set; }                    // from linked ProductCache
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}