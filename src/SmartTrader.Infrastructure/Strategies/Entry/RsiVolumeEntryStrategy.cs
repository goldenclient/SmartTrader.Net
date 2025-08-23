// src/SmartTrader.Infrastructure/Strategies/Entry/RsiVolumeEntryStrategy.cs
using Microsoft.Extensions.Logging;
using Skender.Stock.Indicators;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using SmartTrader.Domain.Enums;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmartTrader.Infrastructure.Strategies.Entry
{
    public class RsiVolumeEntryStrategy : IEntryStrategyHandler
    {
        private readonly ILogger<RsiVolumeEntryStrategy> _logger;
        private readonly IExchangeServiceFactory _exchangeFactory;
        private const int RsiLookbackPeriod = 14;

        public RsiVolumeEntryStrategy(ILogger<RsiVolumeEntryStrategy> logger, IExchangeServiceFactory exchangeFactory)
        {
            _logger = logger;
            _exchangeFactory = exchangeFactory;
        }

        public async Task<StrategySignal> GetSignalAsync(Coin coin, Strategy strategy, string exchangeName)
        {
            var exchangeInfo = coin.GetExchangeInfo().FirstOrDefault(e => e.Exchange.Equals(exchangeName, StringComparison.OrdinalIgnoreCase));
            if (exchangeInfo == null) return new StrategySignal { Reason = "Symbol not found." };

            var marketDataService = _exchangeFactory.CreateService(new Wallet { ApiKey = "LFoqWEuTZpckOqoMTvVyj0tajAmPtdSAzGd0PpZeCh7P14ZTZHtKwvh0etdQszrL", SecretKey = "zRYFyQmIKCeNCKhJUIvYX31pTl5fS3LJNhuVHGdzmSoJ9haq1C960DBRbgTAVtpA" }, new Exchange { ExchangeName = exchangeName });

            // فرض بر این است که GetKlinesAsync داده‌های تایم فریم یک ساعته را برمی‌گرداند
            var klines = (await marketDataService.GetKlinesAsync(exchangeInfo.Symbol)).ToList();

            // برای مقایسه دو مقدار آخر RSI، حداقل به RsiLookbackPeriod + 1 کندل نیاز داریم
            if (klines.Count < RsiLookbackPeriod + 1) return new StrategySignal { Reason = "Not enough kline data." };

            var quotes = klines.Select(k => new Quote
            {
                Date = k.OpenTime,
                Open = k.OpenPrice,
                High = k.HighPrice,
                Low = k.LowPrice,
                Close = k.ClosePrice,
                Volume = k.Volume
            });

            // --- محاسبه RSI ---
            var rsiResults = quotes.GetRsi(RsiLookbackPeriod).ToList();
            if (rsiResults.Count < 2) return new StrategySignal { Reason = "Could not calculate the last two RSI values." };

            var lastRsi = rsiResults.Last();
            var previousRsi = rsiResults[rsiResults.Count - 2];

            // --- دریافت حجم معاملات ---
            var lastVolume = quotes.Last().Volume;
            var previousVolume = quotes.ElementAt(quotes.Count() - 2).Volume;

            // --- بررسی شروط سیگنال Long ---
            if (lastRsi.Rsi < 30 && lastRsi.Rsi > previousRsi.Rsi && lastVolume > previousVolume)
            {
                _logger.LogInformation("RSI Volume Long Signal found for {Symbol}", exchangeInfo.Symbol);
                return new StrategySignal
                {
                    Signal = SignalType.OpenLong,
                    Reason = $"RSI({lastRsi.Rsi:F2}) < 30 and rising, with increasing volume.",
                    // مقادیر از آبجکت استراتژی خوانده می‌شوند
                    PercentBalance = strategy.PercentBalance ?? 5m, // یک مقدار پیش‌فرض در صورت null بودن
                    StopLoss = strategy.StopLoss ?? 5m,
                    TakeProfit = strategy.TakeProfit ?? 5m,
                    Leverage = strategy.Leverage ?? 5
                };
            }

            // --- بررسی شروط سیگنال Short ---
            if (lastRsi.Rsi > 70 && lastRsi.Rsi < previousRsi.Rsi && lastVolume > previousVolume)
            {
                _logger.LogInformation("RSI Volume Short Signal found for {Symbol}", exchangeInfo.Symbol);
                return new StrategySignal
                {
                    Signal = SignalType.OpenShort,
                    Reason = $"RSI({lastRsi.Rsi:F2}) > 70 and falling, with increasing volume.",
                    // مقادیر از آبجکت استراتژی خوانده می‌شوند
                    PercentBalance = strategy.PercentBalance ?? 5m,
                    StopLoss = strategy.StopLoss ?? 5m,
                    TakeProfit = strategy.TakeProfit ?? 5m,
                    Leverage = strategy.Leverage ?? 5
                };
            }

            return new StrategySignal();
        }
    }
}