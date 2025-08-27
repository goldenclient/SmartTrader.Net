using SmartTrader.Domain.Enums;
using System;
// src/SmartTrader.Domain/Entities/Position.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartTrader.Domain.Entities
{
    public class Position
    {
        public int PositionID { get; set; }
        public int WalletID { get; set; }
        public int CoinID { get; set; }
        public int EntryStrategyID { get; set; }
        public int? ExitStrategyID { get; set; }
        public decimal? Stoploss { get; set; }
        public decimal? TakeProfit { get; set; }
        public string Symbol { get; set; }
        public string PositionSide { get; set; }
        public string Status { get; set; } // از Enum استفاده می‌شود
        public decimal EntryPrice { get; set; }
        public decimal EntryValueUSD { get; set; }
        public decimal CurrentQuantity { get; set; }
        public decimal? ProfitUSD { get; set; }
        public DateTime OpenTimestamp { get; set; }
        public DateTime? CloseTimestamp { get; set; }
    }
}