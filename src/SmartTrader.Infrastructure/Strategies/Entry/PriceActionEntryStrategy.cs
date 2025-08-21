using Microsoft.Extensions.Logging;
using Skender.Stock.Indicators;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;

namespace SmartTrader.Infrastructure.Strategies.Entry
{
    public class PriceActionEntryStrategy : IEntryStrategyHandler
    {
        private readonly ILogger<PriceActionEntryStrategy> _logger;
        private readonly IExchangeServiceFactory _exchangeFactory;

        public PriceActionEntryStrategy(ILogger<PriceActionEntryStrategy> logger, IExchangeServiceFactory exchangeFactory)
        {
            _logger = logger;
            _exchangeFactory = exchangeFactory;
        }

        public async Task<StrategySignal> GetSignalAsync(Coin coin, string exchangeName)
        {
            var exchangeInfo = coin.GetExchangeInfo().FirstOrDefault(e => e.Exchange.Equals(exchangeName, StringComparison.OrdinalIgnoreCase));
            if (exchangeInfo == null) return new StrategySignal { Reason = "Symbol not found." };

            var marketDataService = _exchangeFactory.CreateService(new Wallet { ApiKey = "LFoqWEuTZpckOqoMTvVyj0tajAmPtdSAzGd0PpZeCh7P14ZTZHtKwvh0etdQszrL", SecretKey = "zRYFyQmIKCeNCKhJUIvYX31pTl5fS3LJNhuVHGdzmSoJ9haq1C960DBRbgTAVtpA" }, new Exchange { ExchangeName = exchangeName });
            var klines = (await marketDataService.GetKlinesAsync(exchangeInfo.Symbol)).ToList();
            if (klines.Count < 50) return new StrategySignal { Reason = "Not enough kline data for SMA(50)." };

            var quotes = klines.Select(k => new Quote
            {
                Date = k.OpenTime,
                Open = k.OpenPrice,
                High = k.HighPrice,
                Low = k.LowPrice,
                Close = k.ClosePrice,
                Volume = k.Volume
            });

            // --- Condition 1: Bullish Hammer pattern ---
            // کتابخانه Skender به طور مستقیم الگوها را تشخیص می‌دهد
            //var hammerResults = quotes.GetHammer().ToList();
            //var lastHammerSignal = hammerResults.LastOrDefault();
            //bool isHammerPattern = lastHammerSignal?.Match == Match.BullSignal;
            //if (!isHammerPattern)
            //{
            //    return new StrategySignal { Reason = "Hammer pattern not found." };
            //}

            // --- Condition 2: Price is above SMA 50 ---
            var sma50 = quotes.GetSma(50).LastOrDefault();
            var lastCandle = quotes.Last();
            if (sma50?.Sma == null || lastCandle.Close <= (decimal)sma50.Sma)
            {
                return new StrategySignal { Reason = "Price is not above SMA 50." };
            }

            var rsi = quotes.GetRsi(25).LastOrDefault();
            if (rsi?.Rsi>30)
            {
                return new StrategySignal { Reason = "Price is not above RSI(25)<30" };
            }

            // --- Condition 3: Price is in a support zone ---
            if (!IsInSupport(quotes, 20))
            {
                return new StrategySignal { Reason = "Price is not in a support zone." };
            }

            // If all conditions are met
            _logger.LogInformation("Price Action signal found for {Symbol}", exchangeInfo.Symbol);
            return new StrategySignal
            {
                Signal = SignalType.OpenLong,
                Reason = "RSI(25) < 30 and above SMA 50.",
                PercentBalance = 70m,
                StopLoss = 2.0m,
                TakeProfit = 5.0m,
                Leverage = 2
            };
        }

        /// <summary>
        /// Checks if the price is in a support zone based on recent lows.
        /// </summary>
        private static bool IsInSupport(IEnumerable<Quote> history, int lookbackPeriod)
        {
            var recentHistory = history.TakeLast(lookbackPeriod).ToList();
            if (recentHistory.Count < lookbackPeriod) return false;

            decimal lowestLow = recentHistory.Min(q => q.Low);
            decimal lastLow = recentHistory.Last().Low;

            // Price is considered in support if the last low is within 0.5% of the lowest low in the period.
            return lastLow <= (lowestLow * 1.005m);
        }
    }
}