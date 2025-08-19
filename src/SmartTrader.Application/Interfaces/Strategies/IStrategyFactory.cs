// src/SmartTrader.Application/Interfaces/Strategies/IStrategyFactory.cs
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Domain.Entities;

namespace SmartTrader.Application.Interfaces.Strategies
{
    public interface IStrategyFactory
    {
        IStrategyHandler CreateStrategy(Strategy strategy, IExchangeService exchangeService);
    }
}
