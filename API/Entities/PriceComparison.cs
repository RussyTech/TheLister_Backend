using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Entities
{
    public class PriceComparison
    {
        public int Id { get; set; }
        public string UserId { get; set; }           // FK → ApplicationUser
        public string Asin { get; set; }
        public string ProductTitle { get; set; }
        public decimal AmazonPrice { get; set; }
        public decimal EbayLowestNewPrice { get; set; }
        public decimal EbayAverageNewPrice { get; set; }
        public decimal EstimatedEbayFees { get; set; }  // ~13% typically
        public decimal EstimatedProfit { get; set; }
        public decimal ProfitMarginPercent { get; set; }
        public DateTime SnapshotAt { get; set; }

        public ApplicationUser User { get; set; }
    }
}