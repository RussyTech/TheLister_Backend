using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Entities
{
    public class ProductCache
    {
        public int Id { get; set; }
        public string UserId { get; set; }           // FK → ApplicationUser
        public string Asin { get; set; }             // Amazon Standard ID
        public string Title { get; set; }
        public string? Description { get; set; }
        public string? BulletPoints { get; set; }    // JSON array of bullet points
        public string? Brand { get; set; }
        public decimal AmazonPrice { get; set; }
        public string? AmazonUrl { get; set; }
        public string? Category { get; set; }
        public decimal? WeightKg { get; set; }
        public DateTime LastFetchedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        public ApplicationUser User { get; set; }
        public ICollection<ProductImage> Images { get; set; }
        public ICollection<EbayListing> EbayListings { get; set; }
    }
}