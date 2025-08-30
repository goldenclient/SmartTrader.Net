//
// تایم فریم : 5 دقیقه
// معامله لانگ:
// اگر Rsi کندل جاری منهای Rsi کندل قبل بیشتر از 10 بود
// و حجم فعلی بیشتر از حجم کندل قبل بود
// و طول کندل فعلی سه برابر کندل قبل بود
// و اگر کندل فعلی سبز بود وارد معامله لانگ بشه

// معامله شورت :
// اگر Rsi کندل قبل منهای Rsi کندل جاری ، بیشتر از 10 بود
// و حجم فعلی بیشتر از حجم کندل قبل بود
// و طول کندل فعلی سه برابر کندل قبل بود
// و اگر کندل فعلی قرمز بود وارد معامله شورت بشه
// 


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

        public RsiVolumeEntryStrategy(ILogger<RsiVolumeEntryStrategy> logger, IExchangeServiceFactory exchangeFactory)
        {
            _logger = logger;
            _exchangeFactory = exchangeFactory;
        }
        public async Task<StrategySignal> GetSignalAsync(Coin coin, Strategy strategy, string exchangeName)
        {
            var exchangeInfo = coin.GetExchangeInfo().FirstOrDefault(e => e.Exchange.Equals(exchangeName, StringComparison.OrdinalIgnoreCase));
            if (exchangeInfo == null) return new StrategySignal { Reason = "Symbol not found." };

            var marketDataService = _exchangeFactory.CreateService(
                   new Wallet
                   {
                       ApiKey = "LFoqWEuTZpckOqoMTvVyj0tajAmPtdSAzGd0PpZeCh7P14ZTZHtKwvh0etdQszrL",
                       SecretKey = "zRYFyQmIKCeNCKhJUIvYX31pTl5fS3LJNhuVHGdzmSoJ9haq1C960DBRbgTAVtpA"
                   },
                   new Exchange { ExchangeName = exchangeName });
            var klines = (await marketDataService.GetKlinesAsync(exchangeInfo.Symbol, strategy.TimeFrame?.ToString() ?? "5", 50)).ToList();

            // برای تحلیل، حداقل به 4 کندل نیاز داریم
            if (klines.Count < 4) return new StrategySignal { Reason = "Not enough kline data for analysis." };

            var quotes = klines.Select(k => new Quote
            {
                Date = k.OpenTime,
                Open = k.OpenPrice,
                High = k.HighPrice,
                Low = k.LowPrice,
                Close = k.ClosePrice,
                Volume = k.Volume
            }).ToList();

            var rsiList = quotes.GetRsi(14).ToList();
            if (rsiList.Count < 4) return new StrategySignal { Reason = "Not enough RSI data for analysis." };

            // --- منطق جدید و اصلاح‌شده: تصمیم‌گیری بر اساس سن کندل در حال تشکیل ---
            // آخرین کندل در لیست، کندل در حال تشکیل است
            var formingCandleOpenTime = klines.Last().OpenTime;
            var candleAge = DateTime.UtcNow - formingCandleOpenTime;

            // اگر عمر کندل کمتر از ۲۰ ثانیه باشد، آخرین کندل بسته‌شده را بررسی می‌کنیم
            if (candleAge.TotalSeconds > 0 && candleAge.TotalSeconds < 20)
            {
                //_logger.LogInformation("Current candle for {Symbol} is new ({seconds}s old). Analyzing last *closed* candle for safety.",
                    //exchangeInfo.Symbol, (int)candleAge.TotalSeconds);
                // کندل هدف: آخرین بسته‌شده (ماقبل آخر). کندل مقایسه: کندل قبل از آن
                return CheckSignalConditions(quotes[^2], quotes[^3], rsiList[^2], rsiList[^3], strategy, exchangeInfo.Symbol);
            }
            // در غیر این صورت، کندل جاری (بسته نشده) را بررسی می‌کنیم
            else
            {
                //_logger.LogInformation("Current candle for {Symbol} is mature ({seconds}s old). Analyzing *forming* candle for quick reaction.",
                  // exchangeInfo.Symbol, (int)candleAge.TotalSeconds);
                // کندل هدف: کندل جاری و باز (آخرین). کندل مقایسه: کندل قبل از آن (آخرین بسته‌شده)
                return CheckSignalConditions(quotes[^1], quotes[^2], rsiList[^1], rsiList[^2], strategy, exchangeInfo.Symbol);
            }
        }

        private StrategySignal CheckSignalConditions(Quote currentCandle, Quote prevCandle, RsiResult currentRsiResult, RsiResult prevRsiResult, Strategy strategy, string symbol)
        {
            var currentRsi = currentRsiResult.Rsi ?? 0;
            var prevRsi = prevRsiResult.Rsi ?? 0;

            // محاسبه پارامترها
            var rsiDiffUp = currentRsi - prevRsi;
            var rsiDiffDown = prevRsi - currentRsi;
            bool highVolume = currentCandle.Volume > prevCandle.Volume;
            decimal currentRange = Math.Abs(currentCandle.Close - currentCandle.Open);
            decimal prevRange = Math.Abs(prevCandle.Close - prevCandle.Open);
            bool longCandle = prevRange > 0 && currentRange > (prevRange * 3);
            bool isGreen = currentCandle.Close > currentCandle.Open;
            bool isRed = currentCandle.Close < currentCandle.Open;

            // --- شرط ورود Long ---
            var upperShadow = currentCandle.High - Math.Max(currentCandle.Open, currentCandle.Close);
            var lowerShadow = Math.Min(currentCandle.Open, currentCandle.Close) - currentCandle.Low;

            // ---- ورود لانگ ----
            if (rsiDiffUp > 10 && highVolume && longCandle && isGreen && currentRsi < 75 && upperShadow < currentRange * 0.5m)
            {
                _logger.LogInformation("LONG signal for {Symbol}", symbol);
                return new StrategySignal
                {
                    Signal = SignalType.OpenLong,
                    Reason = "RSI jump > 10, high volume, 3x candle length, green candle.",
                    PercentBalance = strategy?.PercentBalance ?? 30m,
                    Leverage = strategy?.Leverage ?? 10
                };
            }

            // --- شرط ورود Short ---
            if (rsiDiffDown > 10 && highVolume && longCandle && isRed && currentRsi > 25 && lowerShadow < currentRange * 0.5m)
            {
                _logger.LogInformation("SHORT signal for {Symbol}", symbol);
                return new StrategySignal
                {
                    Signal = SignalType.OpenShort,
                    Reason = "RSI drop > 10, high volume, 3x candle length, red candle.",
                    PercentBalance = strategy?.PercentBalance ?? 30m,
                    Leverage = strategy?.Leverage ?? 10
                };
            }

            return new StrategySignal { Reason = "No signal condition met on the analyzed candle." };
        }
        //public async Task<StrategySignal> GetSignalAsync(Coin coin, Strategy strategy, string exchangeName)
        //{
        //    var exchangeInfo = coin.GetExchangeInfo()
        //        .FirstOrDefault(e => e.Exchange.Equals(exchangeName, StringComparison.OrdinalIgnoreCase));
        //    if (exchangeInfo == null)
        //        return new StrategySignal { Reason = "Symbol not found." };

        //    var marketDataService = _exchangeFactory.CreateService(
        //        new Wallet
        //        {
        //            ApiKey = "LFoqWEuTZpckOqoMTvVyj0tajAmPtdSAzGd0PpZeCh7P14ZTZHtKwvh0etdQszrL",
        //            SecretKey = "zRYFyQmIKCeNCKhJUIvYX31pTl5fS3LJNhuVHGdzmSoJ9haq1C960DBRbgTAVtpA"
        //        },
        //        new Exchange { ExchangeName = exchangeName });

        //    // ---- دیتای کندل 5 دقیقه ----
        //    var klines = (await marketDataService.GetKlinesAsync(
        //        exchangeInfo.Symbol,
        //        strategy.TimeFrame?.ToString() ?? "5",
        //        50)).ToList();

        //    if (klines.Count < 5)
        //        return new StrategySignal { Reason = "Not enough candles." };

        //    var quotes = klines.Select(k => new Quote
        //    {
        //        Date = k.OpenTime,
        //        Open = k.OpenPrice,
        //        High = k.HighPrice,
        //        Low = k.LowPrice,
        //        Close = k.ClosePrice,
        //        Volume = k.Volume
        //    }).ToList();

        //    // ---- RSI محاسبه ----
        //    var rsiPeriod = 14;
        //    var rsiList = quotes.GetRsi(rsiPeriod).ToList();
        //    if (rsiList.Count < 2)
        //        return new StrategySignal { Reason = "Not enough RSI data." };

        //    var lastRsi = rsiList[^1].Rsi ?? 0;
        //    var prevRsi = rsiList[^2].Rsi ?? 0;

        //    // ---- کندل‌ها ----
        //    var lastCandle = quotes[^1];
        //    var prevCandle = quotes[^2];

        //    // اختلاف RSI
        //    var rsiDiffUp = lastRsi - prevRsi;
        //    var rsiDiffDown = prevRsi - lastRsi;

        //    // حجم
        //    bool highVolume = lastCandle.Volume > (prevCandle.Volume * 1);

        //    // طول کندل
        //    decimal lastRange =Math.Abs(lastCandle.Close - lastCandle.Open);
        //    decimal prevRange =Math.Abs(prevCandle.Close - prevCandle.Open);
        //    bool longCandle = lastRange > (prevRange * 3);

        //    // رنگ کندل
        //    bool isGreen = lastCandle.Close > lastCandle.Open;
        //    bool isRed = lastCandle.Close < lastCandle.Open;

        //    // شرط جدید: بررسی قدرت کندل
        //    var upperShadow = lastCandle.High - Math.Max(lastCandle.Open, lastCandle.Close);
        //    var lowerShadow = Math.Min(lastCandle.Open, lastCandle.Close) - lastCandle.Low;

        //    // ---- ورود لانگ ----
        //    if (rsiDiffUp > 10 && highVolume && longCandle && isGreen && lastRsi < 75 && upperShadow < lastRange * 0.5m)
        //    {
        //        _logger.LogInformation("LONG signal for {Symbol}", exchangeInfo.Symbol);
        //        return new StrategySignal
        //        {
        //            Signal = SignalType.OpenLong,
        //            Reason = "RSI افزایش شدید + حجم ۳ برابر + کندل بلند سبز",
        //            PercentBalance = strategy?.PercentBalance ?? 30m,
        //            Leverage = strategy?.Leverage ?? 10
        //        };
        //    }

        //    // ---- ورود شورت ----
        //    if (rsiDiffDown > 10 && highVolume && longCandle && isRed && lastRsi > 25 && lowerShadow < lastRange * 0.5m)
        //    {
        //        _logger.LogInformation("SHORT signal for {Symbol}", exchangeInfo.Symbol);
        //        return new StrategySignal
        //        {
        //            Signal = SignalType.OpenShort,
        //            Reason = "RSI کاهش شدید + حجم ۳ برابر + کندل بلند قرمز",
        //            PercentBalance = strategy?.PercentBalance ?? 30m,
        //            Leverage = strategy?.Leverage ?? 10
        //        };
        //    }

        //    return new StrategySignal { Reason = "No entry condition met." };
        //}



    }
}