using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.DTOs
{
    public class CategoryAspectDto
    {
        public string Name { get; set; } = string.Empty;          // e.g. "Brand", "Size", "Color"
        public bool Required { get; set; }
        public List<string> SuggestedValues { get; set; } = new(); // e.g. ["S", "M", "L", "XL"]
        public string? AspectType { get; set; }                    // "STRING", "NUMBER", "DATE"

    }
}