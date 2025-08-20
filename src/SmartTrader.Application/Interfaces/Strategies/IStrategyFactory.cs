// src/SmartTrader.Application/Interfaces/Strategies/IStrategyFactory.cs
using SmartTrader.Domain.Entities;

namespace SmartTrader.Application.Interfaces.Strategies
{
    public interface IStrategyFactory
    {
        IEntryStrategyHandler CreateEntryStrategy(Strategy strategy);
        // پارامتر exchangeService حذف شد
        IExitStrategyHandler CreateExitStrategy(Strategy strategy);
    }
}