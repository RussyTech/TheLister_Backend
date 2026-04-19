using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace API.DTOs
{
    public class EndListingRequest
    {
        [Required]
        public string Reason { get; set; } = "NotAvailable";
    }
}