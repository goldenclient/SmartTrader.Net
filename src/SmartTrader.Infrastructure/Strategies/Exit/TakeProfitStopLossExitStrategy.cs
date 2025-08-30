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

        public TakeProfitStopLossExitStrategy(
            ILogger<TakeProfitStopLossExitStrategy> logger,
            IWalletRepository walletRepo,
            IExchangeRepository exchangeRepo,
            IStrategyRepository strategyRepo,
            IExchangeServiceFactory exchangeFactory,
            ITelegramNotifier telegramNotifier)   // ✅ اینجا هم تزریق شد
        {
            _logger = logger;
            _walletRepo = walletRepo;
            _exchangeRepo = exchangeRepo;
            _strategyRepo = strategyRepo;
            _exchangeFactory = exchangeFactory;
            _telegramNotifier = telegramNotifier;
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
            var klines = (await exchangeService.GetKlinesAsync(position.Symbol, strategy.TimeFrame?.ToString() ?? "5", 50)).ToList();
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
            if (position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
                pnlPercentage = (currentPrice - position.EntryPrice) / position.EntryPrice;
            else
                pnlPercentage = (position.EntryPrice - currentPrice) / position.EntryPrice;
            // pnlPercentage = pnlPercentage * 10; // اعمال لوریج بر روی سود
            if (pnlPercentage >= 0.01m) //  0.1m)
            {
                _logger.LogInformation("Take Profit > 10% triggered for Position {PositionID}. PNL: {pnl}%", position.PositionID, pnlPercentage * 100);
                return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "Profit exceeded 5%." };
            }


            if (DateTime.UtcNow > candleClose.AddSeconds(15) &&  position.Stoploss == null)
            {
                var body = Math.Abs(prevCandle.Close - prevCandle.Open);
                if (body > 0) // جلوگیری از تقسیم بر صفر برای کندل‌های دوجی
                {
                    if (position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        var upperShadow = prevCandle.High - Math.Max(prevCandle.Open, prevCandle.Close);
                        if (upperShadow > body * 0.5m)
                        {
                            _logger.LogInformation("Weak candle (long upper shadow) detected for LONG Position {PositionID}", position.PositionID);
                            return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "Weak candle detected (long upper shadow)." };
                        }
                    }
                    else // SHORT
                    {
                        var lowerShadow = Math.Min(prevCandle.Open, prevCandle.Close) - prevCandle.Low;
                        if (lowerShadow > body * 0.5m)
                        {
                            _logger.LogInformation("Weak candle (long lower shadow) detected for SHORT Position {PositionID}", position.PositionID);
                            return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "Weak candle detected (long lower shadow)." };
                        }
                    }
                }
                // تنظیم استاپ لاس ابتدای کندل ورود
                var stoploss = prevCandle.Open;
                return new StrategySignal { Signal = SignalType.ChangeSL, Reason = "Set StopLoss => " + position.Symbol + "=SL:" + stoploss, NewStopLossPrice = stoploss };

            }
            // تنظیم استاپ لاس وسط کندل ورود
            //if (DateTime.UtcNow > candleClose.AddMinutes(1) && position.Stoploss == null)
            //{
            //    var stoploss = (prevCandle.Close + prevCandle.Open) / 2;
            //    return new StrategySignal { Signal = SignalType.ChangeSL, Reason = "Set StopLoss => " + position.Symbol + "=SL:" + stoploss, NewStopLossPrice = stoploss };
            //}

            // شرط مدت نگهداری: حداقل تا پایان کندل بعد از ورود
            if (DateTime.UtcNow < candleClose.AddMinutes(5))
            {
                return new StrategySignal { Signal = SignalType.Hold, Reason = "Minimum holding period not reached." };
            }

            // ----- استراتژی خروج لانگ -----
            if (position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (rsiPrev - rsiCurr > 5)
                    return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "RSI dropped sharply (>5)." };

                if (rsiCurr > 80 && isRed)
                    return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "RSI > 80 with red candle." };

                if (lastCandle.Close < position.Stoploss)
                    return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "StopLoss, down to open first candle" };
            }

            // ----- استراتژی خروج شورت -----
            if (position.PositionSide.Equals(SignalType.OpenShort.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (rsiCurr - rsiPrev > 5)
                    return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "RSI jumped sharply (>5)." };

                if (rsiCurr < 20 && isGreen)
                    return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "RSI < 20 with green candle." };

                if (lastCandle.Close > position.Stoploss)
                    return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "StopLoss, up to open first candle" };
            }

            return new StrategySignal { Signal = SignalType.Hold, Reason = "No exit condition met." };
        }

        private DateTime GetCandleCloseTime(DateTime openTime, int timeframeMinutes = 5)
        {
            var totalMinutes = openTime.Hour * 60 + openTime.Minute;
            var currentCandle = totalMinutes / timeframeMinutes;
            var closeMinutes = (currentCandle + 1) * timeframeMinutes;

            return openTime.Date.AddMinutes(closeMinutes);
        }

    }

}
