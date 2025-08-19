// src/SmartTrader.Application/Interfaces/Persistence/IStrategyRepository.cs
using SmartTrader.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Persistence
{
    public interface IStrategyRepository
    {
        Task<IEnumerable<StrategyTradableCoin>> GetTradableCoinsByStrategyIdAsync(int strategyId);
        Task<IEnumerable<Strategy>> GetAllAsync(); // متد جدید
    }
}
