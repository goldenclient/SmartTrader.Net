// src/SmartTrader.Infrastructure/Strategies/Entry/RsiVolumeEntryStrategy.cs
using Binance.Net.Enums;
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
            var exchangeInfo = coin.GetExchangeInfo()
                .FirstOrDefault(e => e.Exchange.Equals(exchangeName, StringComparison.OrdinalIgnoreCase));
            if (exchangeInfo == null)
                return new StrategySignal { Reason = "Symbol not found." };

            var marketDataService = _exchangeFactory.CreateService(
                new Wallet { ApiKey = "LFoqWEuTZpckOqoMTvVyj0tajAmPtdSAzGd0PpZeCh7P14ZTZHtKwvh0etdQszrL", SecretKey = "zRYFyQmIKCeNCKhJUIvYX31pTl5fS3LJNhuVHGdzmSoJ9haq1C960DBRbgTAVtpA" },
                new Exchange { ExchangeName = exchangeName });

            // ---- پارامترها (از Strategy اگر بود؛ وگرنه پیش‌فرض) ----
            int emaFast = 20;
            int emaSlow = 50;
            int rsiLen = 14;
            int atrLen = 14;
            decimal atrMult = 1.5m;

            // ---- دیتای کندل 15 دقیقه ----
            var klines = (await marketDataService.GetKlinesAsync(exchangeInfo.Symbol, strategy.TimeFrame?.ToString() ?? TimeFrame.FifteenMinute.ToString(),300)).ToList();

            // حداقل تعداد برای اینکه آخرین مقدارهای EMA50/RSI/ATR نال نباشند
            int warmup = Math.Max(Math.Max(emaSlow, rsiLen), atrLen) + 1;
            if (klines.Count < warmup)
                return new StrategySignal { Reason = "Not enough kline data." };

            var quotes = klines.Select(k => new Quote
            {
                Date = k.OpenTime,
                Open = k.OpenPrice,
                High = k.HighPrice,
                Low = k.LowPrice,
                Close = k.ClosePrice,
                Volume = k.Volume
            }).ToList();

            // ---- محاسبه اندیکاتورها ----
            var ema20 = quotes.GetEma(emaFast).ToList();
            var ema50 = quotes.GetEma(emaSlow).ToList();
            var rsi = quotes.GetRsi(rsiLen).ToList();
            var atr = quotes.GetAtr(atrLen).ToList();

            // آخرین کندل بسته‌شده
            int i = quotes.Count - 1;

            decimal lastClose = (decimal)quotes[i].Close;
            decimal lastEma20 = ema20[i].Ema.HasValue ? (decimal)ema20[i].Ema.Value : 0m;
            decimal lastEma50 = ema50[i].Ema.HasValue ? (decimal)ema50[i].Ema.Value : 0m;
            decimal lastRsi = rsi[i].Rsi.HasValue ? (decimal)rsi[i].Rsi.Value : 50m;
            decimal lastAtr = atr[i].Atr.HasValue ? (decimal)atr[i].Atr.Value : 0m;

            if (lastEma20 == 0m || lastEma50 == 0m || lastAtr <= 0m)
                return new StrategySignal { Reason = "Indicator values not ready." };

            // حد ضرر به‌صورت فاصله‌ی قیمتی = ATR * 1.5
            decimal stopDistance = lastAtr * atrMult;

            // برای همخوانی با مدل فعلی، StopLoss را درصدی از قیمت بفرستیم:
            decimal stopLossPercent = (stopDistance / lastClose) * 100m;

            // ---- ورود لانگ: EMA20>EMA50 و RSI>40 ----
            if (lastEma20 > lastEma50 && lastRsi > 40m)
            {
                _logger.LogInformation("LONG signal (EMA+RSI+ATR) for {Symbol}", exchangeInfo.Symbol);
                return new StrategySignal
                {
                    Signal = SignalType.OpenLong,
                    Reason = $"EMA20({lastEma20:F2})>EMA50({lastEma50:F2}), RSI={lastRsi:F2}>40, ATR={lastAtr:F2}, SL≈{stopLossPercent:F2}%",
                    PercentBalance = strategy?.PercentBalance ?? 2m,
                    StopLoss = stopLossPercent,     // درصدی
                    Leverage = strategy?.Leverage ?? 3,
                };
            }

            // ---- ورود شورت: EMA20<EMA50 و RSI<60 ----
            if (lastEma20 < lastEma50 && lastRsi < 60m)
            {
                _logger.LogInformation("SHORT signal (EMA+RSI+ATR) for {Symbol}", exchangeInfo.Symbol);
                return new StrategySignal
                {
                    Signal = SignalType.OpenShort,
                    Reason = $"EMA20({lastEma20:F2})<EMA50({lastEma50:F2}), RSI={lastRsi:F2}<60, ATR={lastAtr:F2}, SL≈{stopLossPercent:F2}%",
                    PercentBalance = strategy?.PercentBalance ?? 2m,
                    StopLoss = stopLossPercent,     // درصدی
                    Leverage = strategy?.Leverage ?? 3
                };
            }

            return new StrategySignal { Reason = "No entry condition met." };
        }
    }
}