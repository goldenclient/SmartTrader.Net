namespace SmartTrader.Application.Models
{
    public enum SignalType { Hold, OpenLong, OpenShort, Close }

    public class StrategySignal
    {
        public SignalType Signal { get; set; } = SignalType.Hold;
        public string Reason { get; set; }
    }
}