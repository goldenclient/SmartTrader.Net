// src/SmartTrader.Infrastructure/Strategies/Exit/FibonacciTrailExitStrategy.cs
using Microsoft.Extensions.Logging;
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
    public class FibonacciTrailExitStrategy : IExitStrategyHandler
    {
        private readonly ILogger<FibonacciTrailExitStrategy> _logger;
        private readonly IWalletRepository _walletRepo;
        private readonly IExchangeRepository _exchangeRepo;
        private readonly IStrategyRepository _strategyRepo;
        private readonly IPositionRepository _positionRepo;
        private readonly IExchangeServiceFactory _exchangeFactory;

        public FibonacciTrailExitStrategy(
            ILogger<FibonacciTrailExitStrategy> logger,
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
            // --- 1. جمع‌آوری داده‌های مورد نیاز ---
            var wallet = (await _walletRepo.GetActiveWalletsAsync()).FirstOrDefault(w => w.WalletID == position.WalletID);
            var exchange = (await _exchangeRepo.GetAllAsync()).FirstOrDefault(e => e.ExchangeID == wallet?.ExchangeID);
            var entryStrategy = (await _strategyRepo.GetAllAsync()).FirstOrDefault(s => s.StrategyID == position.EntryStrategyID);
            var positionHistory = await _positionRepo.GetHistoryByPositionIdAsync(position.PositionID);

            if (wallet == null || exchange == null || entryStrategy == null)
                return new StrategySignal { Reason = "Missing position metadata." };

            var exchangeService = _exchangeFactory.CreateService(wallet, exchange);
            var currentPrice = await exchangeService.GetLastPriceAsync(position.Symbol);
            if (currentPrice == 0) return new StrategySignal { Reason = "Price not available." };

            // --- 2. محاسبه اهداف و حدود ---
            var tpTargetPrice = position.EntryPrice * (1 + (entryStrategy.TakeProfit ?? 5m) / 100);
            var slTargetPrice = position.EntryPrice * (1 - (entryStrategy.StopLoss ?? 2m) / 100);
            var totalTargetRange = tpTargetPrice - position.EntryPrice;

            // --- 3. بررسی شروط استراتژی ---
            if (currentPrice >= tpTargetPrice)
                return new StrategySignal { Signal = SignalType.CloseByTP, Reason = "Take Profit target reached." };
            if (currentPrice <= slTargetPrice)
                return new StrategySignal { Signal = SignalType.CloseBySL, Reason = "Stop Loss target reached." };

            var trailSlTarget = position.EntryPrice + (totalTargetRange * 0.89m);
            if (currentPrice >= trailSlTarget && !positionHistory.Any(h => h.ActionType == ActionType.ChangeSL))
            {
                var newSlPrice = slTargetPrice * 1.144m;
                return new StrategySignal { Signal = SignalType.ChangeSL, NewStopLossPrice = newSlPrice, Reason = "Trailing Stop Loss triggered." };
            }

            var partialSellTarget = position.EntryPrice + (totalTargetRange * 0.34m);
            if (currentPrice >= partialSellTarget && !positionHistory.Any(h => h.ActionType == ActionType.SellProfit))
            {
                return new StrategySignal { Signal = SignalType.SellProfit, PercentPosition = 21, Reason = "Partial profit taken (21%)." };
            }

            bool hasSoldPartial = positionHistory.Any(h => h.ActionType == ActionType.SellProfit);
            if (currentPrice <= position.EntryPrice && hasSoldPartial && !positionHistory.Any(h => h.ActionType == ActionType.BuyRollback && h.Price <= position.EntryPrice))
            {
                return new StrategySignal { Signal = SignalType.BuyRollback, PercentBalance = 13, Reason = "Re-buy at entry point (13%)." };
            }

            var deepBuyTarget = position.EntryPrice * 0.99m;
            if (currentPrice <= deepBuyTarget && !positionHistory.Any(h => h.ActionType == ActionType.BuyRollback && h.Price <= deepBuyTarget))
            {
                return new StrategySignal { Signal = SignalType.BuyRollback, PercentBalance = 55, Reason = "Re-buy on dip (55%)." };
            }

            return new StrategySignal();
        }
    }
}
