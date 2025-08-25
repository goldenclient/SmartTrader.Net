// src/SmartTrader.Application/Models/StrategySignal.cs
namespace SmartTrader.Application.Models
{
    using SmartTrader.Domain.Enums;

    public class StrategySignal
    {
        // بخش مشترک
        public SignalType Signal { get; set; } = SignalType.Hold;
        public string Reason { get; set; }

        // پارامترهای استراتژی ورود
        public decimal? PercentBalance { get; set; }
        public decimal? StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }
        public int? Leverage { get; set; }
        public string Symbol { get; set; }
        public decimal Quantity { get; set; }
        public int? PartialPercent { get; set; }

        // پارامترهای استراتژی خروج
        public decimal? PercentPosition { get; set; }
        public decimal? NewStopLossPrice { get; set; }
    }
}

//// src/SmartTrader.Application/Models/StrategySignal.cs
//namespace SmartTrader.Application.Models
//{
//    using SmartTrader.Domain.Enums;

//    public enum SignalAction { Hold, Execute }

//    public class StrategySignal
//    {
//        // بخش مشترک
//        public SignalAction Action { get; set; } = SignalAction.Hold;
//        public ActionType ActionType { get; set; }
//        public string Reason { get; set; }

//        // پارامترهای استراتژی ورود
//        public decimal? PercentBalance { get; set; }
//        public decimal? StopLoss { get; set; }
//        public decimal? TakeProfit { get; set; }
//        public int? Leverage { get; set; }
//        public string Symbol { get; set; }
//        public decimal Quantity { get; set; }

//        // پارامترهای استراتژی خروج
//        public decimal? PercentPosition { get; set; }
//        public decimal? NewStopLossPrice { get; set; }
//    }
//}