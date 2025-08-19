using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// src/SmartTrader.Domain/Entities/Strategy.cs
namespace SmartTrader.Domain.Entities
{
    public class Strategy
    {
        public int StrategyID { get; set; }
        public string StrategyName { get; set; }
        public string StrategyType { get; set; } // "Entry" or "Exit"
        public string MarketType { get; set; } // "Crypto" or "Forex"
        public string? MarketTrend { get; set; } // "Bullish", "Bearish", "Sideways"
        public decimal? StopLossPercentage { get; set; }
        public decimal? TakeProfitPercentage { get; set; }
        public int? Leverage { get; set; }
        public decimal? BalancePercentToTrade { get; set; }
        public bool IsActive { get; set; }
    }
}