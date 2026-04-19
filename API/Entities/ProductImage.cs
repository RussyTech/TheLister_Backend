using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Entities
{
    public class ProductImage
    {
        public int Id { get; set; }
        public int ProductCacheId { get; set; }      // FK → ProductCache
        public string ImageUrl { get; set; }
        public int SortOrder { get; set; }           // 0 = main image
        public bool IsPrimary { get; set; }

        public ProductCache Product { get; set; }
    }
}