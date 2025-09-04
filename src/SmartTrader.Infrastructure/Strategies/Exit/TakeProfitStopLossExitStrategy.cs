// استراتژی خروج:
// بعد از کندل اول، استاپ را بذار روی وسط کندل اول( کندلی که پوزیشن در آن باز شده )
// مدت نگهداری بدون شرط پوزیشن: از نقطه ورود تا انتهای کندل بعد در تایم فریم 5 دقیقه

// - شرطهای خروج برای لانگ بعد از مدت نگهداری:
// - اگر RSI کندل قبل منهای RSI کندل حاری بیشتر از 5 بود خارج شود
// - اگر RSI بزرگتر از 80 شد و کندل قرمز بود خارج شود

// - شرطهای خروج برای شورت بعد از مدت نگهداری:
// - اگر RSI کندل جاری منهای RSI کندل قبل بیشتر از 5 بود خارج شود
// - اگر RSI کمتر از 20 شد و کندل سبز بود خارج شود

using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Skender.Stock.Indicators;
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using SmartTrader.Domain.Enums;
using SmartTrader.Infrastructure.Services;
using System.Linq;
using System.Threading.Tasks;

namespace SmartTrader.Infrastructure.Strategies.Exit
{
    public class TakeProfitStopLossExitStrategy : IExitStrategyHandler
    {
        private readonly ILogger<TakeProfitStopLossExitStrategy> _logger;
        private readonly IWalletRepository _walletRepo;
        private readonly IExchangeRepository _exchangeRepo;
        private readonly IStrategyRepository _strategyRepo;
        private readonly IExchangeServiceFactory _exchangeFactory;
        private readonly ITelegramNotifier _telegramNotifier;   // ✅ اضافه شد
        private readonly IPositionRepository _positionRepo;

        public TakeProfitStopLossExitStrategy(
            ILogger<TakeProfitStopLossExitStrategy> logger,
            IWalletRepository walletRepo,
            IExchangeRepository exchangeRepo,
            IStrategyRepository strategyRepo,
            IExchangeServiceFactory exchangeFactory,
            ITelegramNotifier telegramNotifier,   // ✅ اینجا هم تزریق شد
            IPositionRepository positionRepo)

        {
            _logger = logger;
            _walletRepo = walletRepo;
            _exchangeRepo = exchangeRepo;
            _strategyRepo = strategyRepo;
            _exchangeFactory = exchangeFactory;
            _telegramNotifier = telegramNotifier;
            _positionRepo = positionRepo;

        }

        public async Task<StrategySignal> ExecuteAsync(Position position)
        {
            // --- گرفتن اطلاعات پایه ---
            var wallet = (await _walletRepo.GetActiveWalletsAsync()).FirstOrDefault(w => w.WalletID == position.WalletID);
            var exchange = (await _exchangeRepo.GetAllAsync()).FirstOrDefault(e => e.ExchangeID == wallet?.ExchangeID);
            var strategy = (await _strategyRepo.GetAllAsync()).FirstOrDefault(s => s.StrategyID == position.ExitStrategyID);

            if (wallet == null || exchange == null || strategy == null)
            {
                _logger.LogWarning("Could not find required entities (wallet, exchange, or strategy) for position {PositionID}", position.PositionID);
                return new StrategySignal { Signal = SignalType.Hold, Reason = "Missing position metadata." };
            }

            var exchangeService = _exchangeFactory.CreateService(wallet, exchange);

            // گرفتن کندل‌های 5 دقیقه‌ای (حداقل 3 کندل برای محاسبه RSI قبل/فعلی)
            var klines = (await exchangeService.GetKlinesAsync(position.Symbol, strategy.TimeFrame?.ToString() ?? "15", 50)).ToList();
            if (klines.Count < 15)
                return new StrategySignal { Signal = SignalType.Hold, Reason = "Not enough candle data." };
            //klines.RemoveAt(klines.Count - 1);

            // تبدیل به Quote برای محاسبات
            var quotes = klines.Select(k => new Quote
            {
                Date = k.OpenTime,
                Open = k.OpenPrice,
                High = k.HighPrice,
                Low = k.LowPrice,
                Close = k.ClosePrice,
                Volume = k.Volume
            }).ToList();

            // محاسبه RSI روی دیتای بسته شدن
            var rsi = quotes.GetRsi(14).ToList();
            if (rsi.Count < 2)
                return new StrategySignal { Signal = SignalType.Hold, Reason = "Not enough RSI data." };

            decimal rsiPrev = (decimal)rsi[^2].Rsi!;
            decimal rsiCurr = (decimal)rsi[^1].Rsi!;

            _logger.LogInformation("Symbol: {symbol} - RSI: {rsi1} , {rsi}", position.Symbol, Math.Round(rsiPrev, 2), Math.Round(rsiCurr, 2));
            var reason = $"Symbol: {position.Symbol} - RSI: {Math.Round(rsiPrev, 2)} , {Math.Round(rsiCurr, 2)}";
            await _telegramNotifier.SendNotificationHistoryAsync(reason);   // ✅ ارسال به History
            // 
            var lastCandle = quotes[^1];
            var prevCandle = quotes[^2];

            bool isGreen = lastCandle.Close > lastCandle.Open;
            bool isRed = lastCandle.Close < lastCandle.Open;

            var candleClose = GetCandleCloseTime(position.OpenTimestamp, 5);

            var currentPrice = lastCandle.Close;

            // --- 2. محاسبه سود لحظه‌ای ---
            decimal pnlPercentage = 0;
            decimal priceChangePercentage = 0;
            if (position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
                priceChangePercentage = (currentPrice - position.EntryPrice) / position.EntryPrice;
            else
                priceChangePercentage = (position.EntryPrice - currentPrice) / position.EntryPrice;

            // اعمال لوریج برای محاسبه سود واقعی
            int leverage = position.Leverage > 0 ? position.Leverage : 1;
            pnlPercentage = priceChangePercentage * leverage;

            if (pnlPercentage >= 0.1m) // 8% profit with leverage
            {
                var positionHistory = await _positionRepo.GetHistoryByPositionIdAsync(position.PositionID);

                // این شرط را فقط یک بار اجرا می‌کنیم
                if (!positionHistory.Any(h => h.ActionType == SignalType.PartialClose && h.Description.Contains("Trailing SL to 5% profit and Sell 50%")))
                {
                    // محاسبه قیمت جدید حد ضرر بر اساس سود 5% با لوریج
                    decimal priceChangeFor5PercentProfit = 0.05m / leverage;
                    decimal newStopLossPrice;
                    if (position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
                        newStopLossPrice = position.EntryPrice * (1 + priceChangeFor5PercentProfit);
                    else // SHORT
                        newStopLossPrice = position.EntryPrice * (1 - priceChangeFor5PercentProfit);

                    _logger.LogInformation("Profit > 10% triggered. Moving SL to 5% profit for Position {PositionID}. New SL: {sl}", position.PositionID, newStopLossPrice);
                    return new StrategySignal
                    {
                        Signal = SignalType.PartialClose,
                        PartialPercent=50,
                        NewStopLossPrice = newStopLossPrice,
                        TakeProfit = position.TakeProfit * 2,
                        Reason = "Trailing SL to 5% profit and Sell 50%"
                    };
                }
            }
            else if (pnlPercentage >= 0.05m) // 8% profit with leverage
            {
                var positionHistory = await _positionRepo.GetHistoryByPositionIdAsync(position.PositionID);

                // این شرط را فقط یک بار اجرا می‌کنیم
                if (!positionHistory.Any(h => h.ActionType == SignalType.PartialClose && h.Description.Contains("Trailing SL to 2.5% profit and Sell 50%")))
                {
                    // محاسبه قیمت جدید حد ضرر بر اساس سود 5% با لوریج
                    decimal priceChangeFor5PercentProfit = 0.025m / leverage;
                    decimal newStopLossPrice;
                    if (position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
                        newStopLossPrice = position.EntryPrice * (1 + priceChangeFor5PercentProfit);
                    else // SHORT
                        newStopLossPrice = position.EntryPrice * (1 - priceChangeFor5PercentProfit);

                    _logger.LogInformation("Profit > 5% triggered. Moving SL to 2.5% profit for Position {PositionID}. New SL: {sl}", position.PositionID, newStopLossPrice);
                    return new StrategySignal
                    {
                        Signal = SignalType.PartialClose,
                        PartialPercent= 50,
                        NewStopLossPrice = newStopLossPrice,
                        TakeProfit = position.TakeProfit * 2,
                        Reason = "Trailing SL to 2.5% profit and Sell 50%"
                    };
                }
            }
            else if (pnlPercentage >= 0.025m) // 8% profit with leverage
            {
                //return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "Trailing SL to 5% profit" };
                var positionHistory = await _positionRepo.GetHistoryByPositionIdAsync(position.PositionID);

                // این شرط را فقط یک بار اجرا می‌کنیم
                if (!positionHistory.Any(h => h.ActionType == SignalType.PartialClose && h.Description.Contains("Trailing SL to 0.5% profit and Sell 30%")))
                {
                    // محاسبه قیمت جدید حد ضرر بر اساس سود 5% با لوریج
                    decimal priceChangeFor5PercentProfit = 0.005m / leverage;
                    decimal newStopLossPrice;
                    if (position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
                        newStopLossPrice = position.EntryPrice * (1 + priceChangeFor5PercentProfit);
                    else // SHORT
                        newStopLossPrice = position.EntryPrice * (1 - priceChangeFor5PercentProfit);

                    _logger.LogInformation("Profit > 2.5% triggered. Moving SL to 0.5% profit for Position {PositionID}. New SL: {sl}", position.PositionID, newStopLossPrice);
                    return new StrategySignal
                    {
                        Signal = SignalType.PartialClose,
                        PartialPercent=30,
                        NewStopLossPrice = newStopLossPrice,
                        TakeProfit = position.TakeProfit * 2,
                        Reason = "Trailing SL to 0.5% profit and Sell 30%"
                    };
                }


                //var positionHistory = await _positionRepo.GetHistoryByPositionIdAsync(position.PositionID);

                //// این شرط را فقط یک بار اجرا می‌کنیم
                //if (!positionHistory.Any(h => h.ActionType == SignalType.ChangeSL && h.Description.Contains("Trailing SL to 2.5% profit")))
                //{
                //    // محاسبه قیمت جدید حد ضرر بر اساس سود 5% با لوریج
                //    decimal priceChangeFor5PercentProfit = 0.01m / leverage;
                //    decimal newStopLossPrice;
                //    if (position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
                //        newStopLossPrice = position.EntryPrice * (1 + priceChangeFor5PercentProfit);
                //    else // SHORT
                //        newStopLossPrice = position.EntryPrice * (1 - priceChangeFor5PercentProfit);

                //    _logger.LogInformation("Profit > 2.5% triggered. Moving SL to 1% profit for Position {PositionID}. New SL: {sl}", position.PositionID, newStopLossPrice);
                //    return new StrategySignal
                //    {
                //        Signal = SignalType.ChangeSL,
                //        NewStopLossPrice = newStopLossPrice,
                //        Reason = "Trailing SL to 2.5% profit"
                //    };
                //}
            }

            //var curclosecandle = GetCandleCloseTime(klines.Last().OpenTime, 5);
            //if (DateTime.UtcNow > candleClose.AddSeconds(10) && DateTime.UtcNow > curclosecandle.AddSeconds(-60) && position.Stoploss == null)
            if (DateTime.UtcNow > candleClose.AddSeconds(10) && position.Stoploss == null)
            {
                var stoploss = prevCandle.Open;

                // اگر لانگ بود و توی سود بود استاپ وسط کندل ابتدایی
                // وگر نه استاپ در شروع کندل
                //if ((isGreen && position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
                //    || (isRed && position.PositionSide.Equals(SignalType.OpenShort.ToString(), StringComparison.OrdinalIgnoreCase)))
                //{
                //    stoploss = (prevCandle.Close + prevCandle.Open) / 2;
                //    _logger.LogInformation("profit in Position {PositionID} and set StopLoss to midlle", position.PositionID);
                //}
                //var body = Math.Abs(prevCandle.Close - prevCandle.Open);
                //if (body > 0) // جلوگیری از تقسیم بر صفر برای کندل‌های دوجی
                //{
                //    // تنظیم استاپ لاس ابتدای کندل ورود

                //    if (position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
                //    {
                //        var upperShadow = prevCandle.High - Math.Max(prevCandle.Open, prevCandle.Close);
                //        if (upperShadow > body * 0.5m)
                //        {
                //            _logger.LogInformation("Weak candle (long upper shadow) detected for LONG Position {PositionID} and StopLoss to midlle", position.PositionID);
                //            //return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "Weak candle detected (long upper shadow)." };
                //            stoploss = (prevCandle.Close + prevCandle.Open) / 2;
                //        }

                //    }
                //    else // SHORT
                //    {
                //        var lowerShadow = Math.Min(prevCandle.Open, prevCandle.Close) - prevCandle.Low;
                //        if (lowerShadow > body * 0.5m)
                //        {
                //            _logger.LogInformation("Weak candle (long lower shadow) detected for SHORT Position {PositionID} and StopLoss to midlle", position.PositionID);
                //            stoploss = (prevCandle.Close + prevCandle.Open) / 2;
                //            //return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "Weak candle detected (long lower shadow)." };
                //        }
                //    }
                //}
                return new StrategySignal { Signal = SignalType.ChangeSL, Reason = "Set StopLoss => " + position.Symbol + "=SL:" + stoploss, NewStopLossPrice = stoploss };

            }
            // تنظیم استاپ لاس وسط کندل ورود
            //if (DateTime.UtcNow > candleClose.AddMinutes(1) && position.Stoploss == null)
            //{
            //    var stoploss = (prevCandle.Close + prevCandle.Open) / 2;
            //    return new StrategySignal { Signal = SignalType.ChangeSL, Reason = "Set StopLoss => " + position.Symbol + "=SL:" + stoploss, NewStopLossPrice = stoploss };
            //}

            //شرط مدت نگهداری: حداقل تا پایان کندل بعد از ورود
            if (DateTime.UtcNow < candleClose.AddMinutes(strategy.TimeFrame ?? 15))
            {
                return new StrategySignal { Signal = SignalType.Hold, Reason = "Minimum holding period not reached." };
            }
            position.ProfitUSD = pnlPercentage;

            // ----- استراتژی خروج لانگ -----
            if (position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (rsiPrev - rsiCurr > 5 && position.ProfitUSD>0)
                    return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "RSI dropped sharply (>5)." };

                if (rsiCurr > 80 && isRed)
                    return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "RSI > 80 with red candle." };

                if (lastCandle.Close < position.Stoploss)
                    return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "StopLoss, down to open first candle" };
            }

            // ----- استراتژی خروج شورت -----
            if (position.PositionSide.Equals(SignalType.OpenShort.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (rsiCurr - rsiPrev > 5 && position.ProfitUSD > 0)
                    return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "RSI jumped sharply (>5)." };

                if (rsiCurr < 20 && isGreen)
                    return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "RSI < 20 with green candle." };

                if (lastCandle.Close > position.Stoploss)
                    return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "StopLoss, up to open first candle" };
            }

            return new StrategySignal { Signal = SignalType.Hold, Reason = "No exit condition met." };
        }

        private DateTime GetCandleCloseTime(DateTime openTime, int timeframeMinutes = 15)
        {
            var totalMinutes = openTime.Hour * 60 + openTime.Minute;
            var currentCandle = totalMinutes / timeframeMinutes;
            var closeMinutes = (currentCandle + 1) * timeframeMinutes;

            return openTime.Date.AddMinutes(closeMinutes);
        }

    }

}
