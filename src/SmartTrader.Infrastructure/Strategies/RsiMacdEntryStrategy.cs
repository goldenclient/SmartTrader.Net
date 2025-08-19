// src/SmartTrader.Infrastructure/Strategies/RsiMacdEntryStrategy.cs
using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using System.Threading.Tasks;

namespace SmartTrader.Infrastructure.Strategies
{
    public class RsiMacdEntryStrategy : IStrategyHandler
    {
        private readonly ILogger<RsiMacdEntryStrategy> _logger;

        public RsiMacdEntryStrategy(ILogger<RsiMacdEntryStrategy> logger)
        {
            _logger = logger;
        }

        public async Task<StrategySignal> ExecuteAsync(StrategyContext context)
        {
            // چون این یک استراتژی ورود است، context.Position همیشه null است.
            // ما از سایر اطلاعات موجود در context استفاده می‌کنیم.
            var rsi = context.Rsi;
            // var macd = context.Macd; // فرض کنید MACD را هم محاسبه کرده‌ایم

            if (rsi > 70) // مثال: اگر RSI بالای ۷۰ بود، سیگنال فروش بده
            {
                _logger.LogInformation("RSI > 70. Signal: OpenShort for {Symbol}", context.Symbol);
                return new StrategySignal { Signal = SignalType.OpenShort, Reason = "RSI is overbought." };
            }

            if (rsi < 30) // مثال: اگر RSI زیر ۳۰ بود، سیگنال خرید بده
            {
                _logger.LogInformation("RSI < 30. Signal: OpenLong for {Symbol}", context.Symbol);
                return new StrategySignal { Signal = SignalType.OpenLong, Reason = "RSI is oversold." };
            }

            return new StrategySignal { Signal = SignalType.Hold };
        }
    }
}