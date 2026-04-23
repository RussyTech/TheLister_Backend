using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.Config
{
    public class KeepaSettings
    {
        public string ApiKey { get; set; } = "";
    }
    public class DealScannerSettings
    {
        public int ScanIntervalMinutes { get; set; } = 60;
        public decimal ShippingCostGbp { get; set; } = 3.50m;
        public decimal EbayFinalValueFeePct { get; set; } = 12.8m;
        public decimal EbayFixedFeeGbp { get; set; } = 0.30m;
        public decimal MinProfitThresholdGbp { get; set; } = 1.00m;
    }
}