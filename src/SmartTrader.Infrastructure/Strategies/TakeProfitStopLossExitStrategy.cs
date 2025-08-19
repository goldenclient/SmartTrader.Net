// src/SmartTrader.Infrastructure/Strategies/TakeProfitStopLossExitStrategy.cs
using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using System.Threading.Tasks;

namespace SmartTrader.Infrastructure.Strategies
{
    public class TakeProfitStopLossExitStrategy : IStrategyHandler
    {
        private readonly ILogger<TakeProfitStopLossExitStrategy> _logger;
        private readonly IExchangeService _exchangeService; // این وابستگی توسط Factory مدیریت خواهد شد

        public TakeProfitStopLossExitStrategy(ILogger<TakeProfitStopLossExitStrategy> logger, IExchangeService exchangeService)
        {
            _logger = logger;
            _exchangeService = exchangeService;
        }

        public async Task<StrategySignal> ExecuteAsync(StrategyContext context)
        {
            // تمام اطلاعات مورد نیاز از context در دسترس است
            var position = context.Position;
            var strategy = context.Strategy;
            var pnlPercentage = context.CurrentPnlPercentage;

            if (!pnlPercentage.HasValue)
            {
                return new StrategySignal { Signal = SignalType.Hold, Reason = "PNL not calculated." };
            }

            if (strategy.TakeProfitPercentage.HasValue && pnlPercentage.Value >= strategy.TakeProfitPercentage.Value / 100)
            {
                _logger.LogInformation("Take Profit triggered for position {PositionID}", position.PositionID);
                return new StrategySignal { Signal = SignalType.Close, Reason = "Take Profit reached." };
            }

            if (strategy.StopLossPercentage.HasValue && pnlPercentage.Value <= -strategy.StopLossPercentage.Value / 100)
            {
                _logger.LogInformation("Stop Loss triggered for position {PositionID}", position.PositionID);
                return new StrategySignal { Signal = SignalType.Close, Reason = "Stop Loss reached." };
            }

            return new StrategySignal { Signal = SignalType.Hold };
        }
    }
}
