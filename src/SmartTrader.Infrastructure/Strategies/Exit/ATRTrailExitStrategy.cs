using Binance.Net.Enums;
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
    public class AtrTrailExitStrategy : IExitStrategyHandler
    {
        private readonly ILogger<AtrTrailExitStrategy> _logger;
        private readonly IWalletRepository _walletRepo;
        private readonly IExchangeRepository _exchangeRepo;
        private readonly IStrategyRepository _strategyRepo;
        private readonly IPositionRepository _positionRepo;
        private readonly IExchangeServiceFactory _exchangeFactory;

        private const int AtrPeriod = 14;

        public AtrTrailExitStrategy(
            ILogger<AtrTrailExitStrategy> logger,
            IWalletRepository walletRepo,
            IExchangeRepository exchangeRepo,
            IStrategyRepository strategyRepo,
            IPositionRepository positionRepo,
            IExchangeServiceFactory exchangeFactory)
        {
            _logger = logger;
            _walletRepo = walletRepo;
            _exchangeRepo = exchangeRepo;
            _strategyRepo = strategyRepo;
            _positionRepo = positionRepo;
            _exchangeFactory = exchangeFactory;
        }

        public async Task<StrategySignal> ExecuteAsync(Position position)
        {
            var wallet = (await _walletRepo.GetActiveWalletsAsync()).FirstOrDefault(w => w.WalletID == position.WalletID);
            var exchange = (await _exchangeRepo.GetAllAsync()).FirstOrDefault(e => e.ExchangeID == wallet?.ExchangeID);
            var entryStrategy = (await _strategyRepo.GetAllAsync()).FirstOrDefault(s => s.StrategyID == position.EntryStrategyID);
            var positionHistory = await _positionRepo.GetHistoryByPositionIdAsync(position.PositionID);

            if (wallet == null || exchange == null || entryStrategy == null)
                return new StrategySignal { Reason = "Missing position metadata." };

            var exchangeService = _exchangeFactory.CreateService(wallet, exchange);

            var klines = (await exchangeService.GetKlinesAsync(position.Symbol, TimeFrame.FifteenMinute.ToString(), AtrPeriod + 50)).ToList();
            if (klines.Count < AtrPeriod + 5) return new StrategySignal { Reason = "Not enough kline data." };

            var quotes = klines.Select(k => new Quote
            {
                Date = k.OpenTime,
                Open = k.OpenPrice,
                High = k.HighPrice,
                Low = k.LowPrice,
                Close = k.ClosePrice,
                Volume = k.Volume
            }).ToList();

            var atrResults = quotes.GetAtr(AtrPeriod).ToList();
            var ema20 = quotes.GetEma(20).ToList();
            var ema50 = quotes.GetEma(50).ToList();

            var lastAtr = (decimal)(atrResults.Last().Atr ?? 0);
            var lastEma20 = (decimal)(ema20.Last().Ema ?? 0);
            var lastEma50 = (decimal)(ema50.Last().Ema ?? 0);
            var currentPrice = quotes.Last().Close;

            decimal stopLossPrice, takeProfitPrice;

            if (position.PositionSide == "LONG")
            {
                stopLossPrice = position.EntryPrice - (lastAtr * 1.5m);
                takeProfitPrice = position.EntryPrice + (lastAtr * 3m);
            }
            else
            {
                stopLossPrice = position.EntryPrice + (lastAtr * 1.5m);
                takeProfitPrice = position.EntryPrice - (lastAtr * 3m);
            }

            // --- خروج بر اساس SL / TP ---
            if (position.PositionSide == "LONG")
            {
                if (currentPrice <= stopLossPrice)
                    return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "ATR Stop Loss hit." };

                if (currentPrice >= takeProfitPrice)
                    return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "ATR Take Profit hit." };

                // کراس EMA معکوس
                if (lastEma20 < lastEma50)
                    return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "EMA20 crossed below EMA50 (trend reversal)." };

                // فروش پله‌ای
                var profitDistance = currentPrice - position.EntryPrice;

                if (profitDistance >= lastAtr * 1.5m && !positionHistory.Any(h => h.ActionType == SignalType.PartialClose1))
                    return new StrategySignal { Signal = SignalType.PartialClose, PartialPercent = 25, Reason = "First partial TP at 1.5×ATR" };

                if (profitDistance >= lastAtr * 2m && !positionHistory.Any(h => h.ActionType == SignalType.PartialClose2))
                    return new StrategySignal { Signal = SignalType.PartialClose, PartialPercent = 25, Reason = "Second partial TP at 2×ATR" };

                // تریلینگ استاپ
                if (profitDistance >= lastAtr * 2 && !positionHistory.Any(h => h.ActionType == SignalType.ChangeSL))
                {
                    var newSl = position.EntryPrice + lastAtr;
                    return new StrategySignal { Signal = SignalType.ChangeSL, NewStopLossPrice = newSl, Reason = "Trailing SL moved up." };
                }
            }
            else // SHORT
            {
                if (currentPrice >= stopLossPrice)
                    return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "ATR Stop Loss hit." };

                if (currentPrice <= takeProfitPrice)
                    return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "ATR Take Profit hit." };

                if (lastEma20 > lastEma50)
                    return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "EMA20 crossed above EMA50 (trend reversal)." };

                var profitDistance = position.EntryPrice - currentPrice;

                if (profitDistance >= lastAtr * 1.5m && !positionHistory.Any(h => h.ActionType == SignalType.PartialClose1))
                    return new StrategySignal { Signal = SignalType.PartialClose, PartialPercent = 25, Reason = "First partial TP at 1.5×ATR" };

                if (profitDistance >= lastAtr * 2m && !positionHistory.Any(h => h.ActionType == SignalType.PartialClose2))
                    return new StrategySignal { Signal = SignalType.PartialClose, PartialPercent = 25, Reason = "Second partial TP at 2×ATR" };

                if (profitDistance >= lastAtr * 2 && !positionHistory.Any(h => h.ActionType == SignalType.ChangeSL))
                {
                    var newSl = position.EntryPrice - lastAtr;
                    return new StrategySignal { Signal = SignalType.ChangeSL, NewStopLossPrice = newSl, Reason = "Trailing SL moved down." };
                }
            }

            return new StrategySignal();
        }
    }
}
