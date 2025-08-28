// src/SmartTrader.Domain/Entities/Strategy.cs
namespace SmartTrader.Domain.Entities
{
    public class Strategy
    {
        public int StrategyID { get; set; }
        public string StrategyName { get; set; }
        public bool IsEntryStrategy { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public decimal? PercentBalance { get; set; }
        public decimal? StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }
        public int? Leverage { get; set; }
        public int? TimeFrame { get; set; }
        public bool? OnlyOne { get; set; }

    }
}
