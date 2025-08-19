using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartTrader.Domain.Entities
{
    public class Exchange
    {
        public int ExchangeID { get; set; }
        public string ExchangeName { get; set; }
        public string MarketType { get; set; }
        public string? ApiBaseUrl { get; set; }
    }
}