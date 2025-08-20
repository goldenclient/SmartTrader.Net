// src/SmartTrader.Infrastructure/Strategies/StrategyFactory.cs
using Microsoft.Extensions.DependencyInjection; // using جدید
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Domain.Entities;
using SmartTrader.Infrastructure.Strategies.Entry;
using SmartTrader.Infrastructure.Strategies.Exit;
using System;

namespace SmartTrader.Infrastructure.Strategies
{
    public class StrategyFactory : IStrategyFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public StrategyFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IEntryStrategyHandler CreateEntryStrategy(Strategy strategy)
        {
            switch (strategy.StrategyName)
            {
                case "RsiMacdEntry":
                    return _serviceProvider.GetRequiredService<RsiMacdEntryStrategy>();
                default:
                    throw new NotSupportedException($"Entry Strategy '{strategy.StrategyName}' is not supported.");
            }
        }

        public IExitStrategyHandler CreateExitStrategy(Strategy strategy)
        {
            switch (strategy.StrategyName)
            {
                case "TakeProfitStopLossExit":
                    // استراتژی با تمام وابستگی‌هایش از DI Container گرفته می‌شود
                    return _serviceProvider.GetRequiredService<TakeProfitStopLossExitStrategy>();
                default:
                    throw new NotSupportedException($"Exit Strategy '{strategy.StrategyName}' is not supported.");
            }
        }
    }
}