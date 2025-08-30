// src/SmartTrader.Infrastructure/Strategies/Entry/PriceActionEntryStrategy.cs
using Microsoft.Extensions.Logging;
using Skender.Stock.Indicators;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using SmartTrader.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public async Task<StrategySignal> GetSignalAsync(Coin coin, Strategy strategy, string exchangeName)
        {
            var exchangeInfo = coin.GetExchangeInfo().FirstOrDefault(e => e.Exchange.Equals(exchangeName, StringComparison.OrdinalIgnoreCase));
            if (exchangeInfo == null) return new StrategySignal { Reason = "Symbol not found." };

            var marketDataService = _exchangeFactory.CreateService(new Wallet { ApiKey = "", SecretKey = "" }, new Exchange { ExchangeName = exchangeName });
            var klines = (await marketDataService.GetKlinesAsync(exchangeInfo.Symbol,"15",300)).ToList();
            if (klines.Count < 50) return new StrategySignal { Reason = "Not enough kline data." };

            var quotes = klines.Select(k => new Quote
            {
                Date = k.OpenTime,
                Open = k.OpenPrice,
                High = k.HighPrice,
                Low = k.LowPrice,
                Close = k.ClosePrice,
                Volume = k.Volume
            });

            var rsi = quotes.GetRsi(14).LastOrDefault();

            // --- بررسی شروط سیگنال Long ---
            if (rsi?.Rsi < 30 && IsInSupport(quotes, 20))
            {
                _logger.LogInformation("Price Action LONG signal found for {Symbol}", exchangeInfo.Symbol);
                return new StrategySignal
                {
                    Signal = SignalType.OpenLong,
                    Reason = "RSI < 30 and in support zone.",
                    PercentBalance = strategy.PercentBalance ?? 5m,
                    StopLoss = strategy.StopLoss ?? 5m,
                    TakeProfit = strategy.TakeProfit ?? 5m,
                    Leverage = strategy.Leverage ?? 5
                };
            }

            // --- بررسی شروط سیگنال Short ---
            if (rsi?.Rsi > 70 && IsInResistance(quotes, 20))
            {
                _logger.LogInformation("Price Action SHORT signal found for {Symbol}", exchangeInfo.Symbol);
                return new StrategySignal
                {
                    Signal = SignalType.OpenShort,
                    Reason = "RSI > 70 and in resistance zone.",
                    PercentBalance = strategy.PercentBalance ?? 5m,
                    StopLoss = strategy.StopLoss ?? 5m,
                    TakeProfit = strategy.TakeProfit ?? 5m,
                    Leverage = strategy.Leverage ?? 5
                };
            }

            return new StrategySignal();
        }

        private bool IsInSupport(IEnumerable<Quote> history, int lookbackPeriod)
        {
            var recentHistory = history.TakeLast(lookbackPeriod).ToList();
            if (recentHistory.Count < lookbackPeriod) return false;
            decimal lowestLow = recentHistory.Min(q => q.Low);
            decimal lastLow = recentHistory.Last().Low;
            return lastLow <= (lowestLow * 1.005m);
        }

        private bool IsInResistance(IEnumerable<Quote> history, int lookbackPeriod)
        {
            var recentHistory = history.TakeLast(lookbackPeriod).ToList();
            if (recentHistory.Count < lookbackPeriod) return false;
            decimal highestHigh = recentHistory.Max(q => q.High);
            decimal lastHigh = recentHistory.Last().High;
            return lastHigh >= (highestHigh * 0.995m);
        }
    }
}