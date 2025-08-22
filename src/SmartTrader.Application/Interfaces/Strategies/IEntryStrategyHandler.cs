// src/SmartTrader.Application/Interfaces/Strategies/IEntryStrategyHandler.cs
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Strategies
{
    public interface IEntryStrategyHandler
    {
        // آبجکت Strategy به عنوان پارامتر اضافه شد
        Task<StrategySignal> GetSignalAsync(Coin coin, Strategy strategy, string exchangeName);
    }
}