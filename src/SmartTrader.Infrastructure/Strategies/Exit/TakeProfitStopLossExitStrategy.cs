// استراتژی خروج:
// بعد از کندل اول، استاپ را بذار روی وسط کندل اول( کندلی که پوزیشن در آن باز شده )
// مدت نگهداری بدون شرط پوزیشن: از نقطه ورود تا انتهای کندل بعد در تایم فریم 5 دقیقه

// - شرطهای خروج برای لانگ بعد از مدت نگهداری:
// - اگر RSI کندل قبل منهای RSI کندل حاری بیشتر از 5 بود خارج شود
// - اگر RSI بزرگتر از 80 شد و کندل قرمز بود خارج شود

// - شرطهای خروج برای شورت بعد از مدت نگهداری:
// - اگر RSI کندل جاری منهای RSI کندل قبل بیشتر از 5 بود خارج شود
// - اگر RSI کمتر از 20 شد و کندل سبز بود خارج شود

using Microsoft.Extensions.Logging;
using Skender.Stock.Indicators;
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using SmartTrader.Domain.Enums;
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

        public TakeProfitStopLossExitStrategy(
            ILogger<TakeProfitStopLossExitStrategy> logger,
            IWalletRepository walletRepo,
            IExchangeRepository exchangeRepo,
            IStrategyRepository strategyRepo,
            IExchangeServiceFactory exchangeFactory)
        {
            _logger = logger;
            _walletRepo = walletRepo;
            _exchangeRepo = exchangeRepo;
            _strategyRepo = strategyRepo;
            _exchangeFactory = exchangeFactory;
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
            var klines = (await exchangeService.GetKlinesAsync(position.Symbol, TimeFrame.FiveMinute.ToString(), 50)).ToList();
            if (klines.Count < 5)
                return new StrategySignal { Signal = SignalType.Hold, Reason = "Not enough candle data." };

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

            _logger.LogInformation("Symbol: {symbol} - RSI: {rsi1} , {rsi}", position.Symbol, Math.Round(rsiPrev, 2), Math.Round(rsiCurr,2));
            var lastCandle = quotes[^1];
            var prevCandle = quotes[^2];

            bool isGreen = lastCandle.Close > lastCandle.Open;
            bool isRed = lastCandle.Close < lastCandle.Open;

            if (DateTime.UtcNow > position.OpenTimestamp.AddMinutes(5) && position.Stoploss == null)
            {
                var stoploss = (prevCandle.Close + prevCandle.Open) / 2;
                return new StrategySignal { Signal = SignalType.ChangeSL, Reason = "Set StopLoss => " + position.Symbol + "=SL:" + position.Stoploss, NewStopLossPrice = stoploss };
            }

            // شرط مدت نگهداری: حداقل تا پایان کندل بعد از ورود
            if (DateTime.UtcNow < position.OpenTimestamp.AddMinutes(10))
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
                    return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "StopLoss, down to midlle first candle" };
            }

            // ----- استراتژی خروج شورت -----
            if (position.PositionSide.Equals(SignalType.OpenLong.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                if (rsiCurr - rsiPrev > 5)
                    return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "RSI jumped sharply (>5)." };

                if (rsiCurr < 20 && isGreen)
                    return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "RSI < 20 with green candle." };

                if (lastCandle.Close > position.Stoploss)
                    return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "StopLoss, up to midlle first candle" };
            }

            return new StrategySignal { Signal = SignalType.Hold, Reason = "No exit condition met." };
        }

    }
}
