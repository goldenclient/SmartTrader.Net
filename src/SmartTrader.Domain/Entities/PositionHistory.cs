// src/SmartTrader.Domain/Entities/PositionHistory.cs
using SmartTrader.Domain.Enums;
using System;

namespace SmartTrader.Domain.Entities
{
    public class PositionHistory
    {
        public long PositionHistoryID { get; set; }
        public int PositionID { get; set; }
        public SignalType ActionType { get; set; }
        public decimal? PercentPosition { get; set; }
        public decimal? PercentBalance { get; set; }
        public decimal Price { get; set; }
        public decimal? Profit { get; set; }
        public DateTime ActionTimestamp { get; set; }
        public string? Description { get; set; }
    }
}