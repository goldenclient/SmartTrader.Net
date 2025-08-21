// src/SmartTrader.Infrastructure/Strategies/Entry/RsiMacdEntryStrategy.cs
using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;

namespace SmartTrader.Infrastructure.Strategies.Entry
{
    public class RsiMacdEntryStrategy : IEntryStrategyHandler
    {
        private readonly ILogger<RsiMacdEntryStrategy> _logger;
        private readonly IExchangeServiceFactory _exchangeFactory;

        public RsiMacdEntryStrategy(ILogger<RsiMacdEntryStrategy> logger, IExchangeServiceFactory exchangeFactory)
        {
            _logger = logger;
            _exchangeFactory = exchangeFactory;
        }

        public async Task<StrategySignal> GetSignalAsync(Coin coin, string exchangeName)
        {
            var exchangeInfo = coin.GetExchangeInfo().FirstOrDefault(e => e.Exchange.Equals(exchangeName, StringComparison.OrdinalIgnoreCase));
            if (exchangeInfo == null)
                return new StrategySignal { Reason = "Symbol not found for this exchange." };

            // یک سرویس موقت برای دریافت داده‌های عمومی بازار (بدون نیاز به کلید API)
            var marketDataService = _exchangeFactory.CreateService(new Wallet { ApiKey = "LFoqWEuTZpckOqoMTvVyj0tajAmPtdSAzGd0PpZeCh7P14ZTZHtKwvh0etdQszrL", SecretKey = "zRYFyQmIKCeNCKhJUIvYX31pTl5fS3LJNhuVHGdzmSoJ9haq1C960DBRbgTAVtpA" }, new Exchange { ExchangeName = exchangeName });

            var klines = await marketDataService.GetKlinesAsync(exchangeInfo.Symbol);
            if (!klines.Any()) return new StrategySignal { Reason = "Kline data not available." };

            // ... منطق محاسبه اندیکاتورها ...
            double rsi = 50; // مقدار نمونه

            if (rsi < 30)
            {
                _logger.LogInformation("Signal found for {Symbol}", exchangeInfo.Symbol);
                // پارامترهای معامله در خود استراتژی تعریف (هاردکد) می‌شوند
                return new StrategySignal
                {
                    Signal = SignalType.OpenLong,
                    Reason = "RSI is oversold.",
                    PercentBalance = 5.0m,
                    StopLoss = 2.0m,
                    TakeProfit = 5.0m,
                    Leverage = 10
                };
            }
            return new StrategySignal();
        }
    }
}
