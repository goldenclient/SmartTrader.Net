// src/SmartTrader.Infrastructure/Strategies/StrategyFactory.cs
using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Domain.Entities;
using System;

namespace SmartTrader.Infrastructure.Strategies
{
    public class StrategyFactory : IStrategyFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;

        public StrategyFactory(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            _serviceProvider = serviceProvider;
            _loggerFactory = loggerFactory;
        }

        public IStrategyHandler CreateStrategy(Strategy strategy, IExchangeService exchangeService)
        {
            // بر اساس نام استراتژی در دیتابیس، کلاس مربوطه را برمی‌گردانیم
            // این روش نیاز به ثبت تک تک استراتژی‌ها در Program.cs دارد
            switch (strategy.StrategyName)
            {
                case "TakeProfitStopLossExit":
                    var logger = _loggerFactory.CreateLogger<TakeProfitStopLossExitStrategy>();
                    return new TakeProfitStopLossExitStrategy(logger, exchangeService);

                // case "RsiMacdEntry":
                //     return _serviceProvider.GetRequiredService<RsiMacdEntryStrategy>();

                default:
                    throw new NotSupportedException($"Strategy '{strategy.StrategyName}' is not supported.");
            }
        }
    }
}
