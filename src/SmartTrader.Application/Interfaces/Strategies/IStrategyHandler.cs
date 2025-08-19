// src/SmartTrader.Application/Interfaces/Strategies/IStrategyHandler.cs
using SmartTrader.Application.Models;
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Strategies
{
    public interface IStrategyHandler
    {
        // امضای متد تغییر کرد
        Task<StrategySignal> ExecuteAsync(StrategyContext context);
    }
}