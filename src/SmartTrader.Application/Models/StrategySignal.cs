// src/SmartTrader.Application/Models/StrategySignal.cs
namespace SmartTrader.Application.Models
{
    public enum SignalType { Hold, OpenLong, OpenShort, Close }

    public class StrategySignal
    {
        public SignalType Signal { get; set; } = SignalType.Hold;
        public string Reason { get; set; }

        // فیلدهای جدید برای استراتژی‌های ورود
        public decimal? PercentBalance { get; set; }
        public decimal? StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }
        public int? Leverage { get; set; }
        public string Symbol { get; set; }
        public decimal Quantity { get; set; }
    }
}
