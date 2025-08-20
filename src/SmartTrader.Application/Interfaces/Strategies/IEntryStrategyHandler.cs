// src/SmartTrader.Application/Interfaces/Strategies/IEntryStrategyHandler.cs
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Strategies
{
    public interface IEntryStrategyHandler
    {
        // استراتژی سیگنال را برای یک کوین خاص در یک صرافی مشخص تولید می‌کند
        Task<StrategySignal> GetSignalAsync(Coin coin, string exchangeName);
    }
}