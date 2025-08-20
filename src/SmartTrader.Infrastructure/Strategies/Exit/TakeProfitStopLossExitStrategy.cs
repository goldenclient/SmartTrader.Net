// src/SmartTrader.Infrastructure/Strategies/Exit/TakeProfitStopLossExitStrategy.cs (کاملاً جدید)
using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
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
            // استراتژی اکنون مسئول جمع‌آوری داده‌های مورد نیاز خود است
            // نکته: برای بهینه‌سازی، می‌توان این اطلاعات را در Worker کش کرد و به استراتژی پاس داد
            var wallet = (await _walletRepo.GetActiveWalletsAsync()).FirstOrDefault(w => w.WalletID == position.WalletID);
            var exchange = (await _exchangeRepo.GetAllAsync()).FirstOrDefault(e => e.ExchangeID == wallet?.ExchangeID);
            var strategy = (await _strategyRepo.GetAllAsync()).FirstOrDefault(s => s.StrategyID == position.ExitStrategyID);

            if (wallet == null || exchange == null || strategy == null)
            {
                _logger.LogWarning("Could not find required entities (wallet, exchange, or strategy) for position {PositionID}", position.PositionID);
                return new StrategySignal { Signal = SignalType.Hold, Reason = "Missing position metadata." };
            }

            var exchangeService = _exchangeFactory.CreateService(wallet, exchange);
            var lastPrice = await exchangeService.GetLastPriceAsync(position.Symbol);
            if (lastPrice == 0) return new StrategySignal { Signal = SignalType.Hold, Reason = "Price not available." };

            // ... منطق استراتژی ...
            // در اینجا باید StopLoss و TakeProfit را از جایی بخوانید (مثلاً از Description استراتژی یا هاردکد)
            decimal takeProfitPercent = 5.0m;
            decimal stopLossPercent = 2.0m;

            decimal pnlPercentage = (position.PositionSide == "LONG")
                ? (lastPrice - position.EntryPrice) / position.EntryPrice
                : (position.EntryPrice - lastPrice) / position.EntryPrice;

            if (pnlPercentage * 100 >= takeProfitPercent)
            {
                return new StrategySignal { Signal = SignalType.Close, Reason = "Take Profit reached." };
            }
            if (pnlPercentage * 100 <= -stopLossPercent)
            {
                return new StrategySignal { Signal = SignalType.Close, Reason = "Stop Loss reached." };
            }

            return new StrategySignal();
        }
    }
}
