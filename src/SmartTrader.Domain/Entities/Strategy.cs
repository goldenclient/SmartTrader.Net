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
    }
}
