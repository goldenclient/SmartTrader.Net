// src/SmartTrader.Application/Interfaces/Strategies/IStrategyHandler.cs
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Strategies
{
    public interface IStrategyHandler
    {
        Task<StrategySignal> ExecuteAsync(Position position, Wallet wallet, Strategy strategy);
    }
}
