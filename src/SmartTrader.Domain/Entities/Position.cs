using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// src/SmartTrader.Domain/Entities/Position.cs
using System;

namespace SmartTrader.Domain.Entities
{
    public class Position
    {
        public int PositionID { get; set; }
        public int WalletID { get; set; }
        public int CoinID { get; set; }
        public int EntryStrategyID { get; set; }
        public int? ExitStrategyID { get; set; }
        public string Symbol { get; set; }
        public string PositionSide { get; set; } // "LONG" or "SHORT"
        public string Status { get; set; } // "OPEN" or "CLOSED"
        public decimal EntryPrice { get; set; }
        public decimal EntryValueUSD { get; set; }
        public decimal CurrentQuantity { get; set; }
        public decimal? ProfitUSD { get; set; }
        public DateTime OpenTimestamp { get; set; }
        public DateTime? CloseTimestamp { get; set; }
    }
}