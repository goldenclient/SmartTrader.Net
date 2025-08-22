// src/SmartTrader.Application/Interfaces/Services/ITelegramNotifier.cs
using SmartTrader.Application.Models;
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Services
{
    public interface ITelegramNotifier
    {
        Task SendNotificationAsync(StrategySignal signal, string coinName, string strategyName);
    }
}