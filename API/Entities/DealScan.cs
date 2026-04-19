using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Entities
{
    public class DealScan
    {
        public int Id { get; set; }
        public string UserId { get; set; }           // FK → ApplicationUser
        public string Asin { get; set; }
        public string ProductTitle { get; set; }
        public string? ImageUrl { get; set; }
        public string? Category { get; set; }
        public decimal AmazonPrice { get; set; }
        public decimal EbayAveragePrice { get; set; }
        public decimal ProfitMarginPercent { get; set; }
        public bool IsNotified { get; set; }
        public DateTime ScannedAt { get; set; }

        public ApplicationUser User { get; set; }
    }
}