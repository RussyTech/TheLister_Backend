using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Entities
{
    public class AmazonConnection
    {
        public int Id { get; set; }
        public string UserId { get; set; }           // FK → ApplicationUser
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime AccessTokenExpiresAt { get; set; }
        public string SellerId { get; set; }         // Amazon Merchant/Seller ID
        public string MarketplaceId { get; set; }    // e.g. ATVPDKIKX0DER (US)
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ApplicationUser User { get; set; }
    }
}