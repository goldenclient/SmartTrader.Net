// src/SmartTrader.Application/Interfaces/Services/ITelegramNotifier.cs
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Services
{
    public interface ITelegramNotifier
    {
        Task SendNotificationAsync(StrategySignal signal, string coinName, string strategyName,string walletName,decimal price);
        Task SendNotificationCloseAsync(StrategySignal signal,string walletName,decimal actionPrice, Position position );
        Task SendNotificationHistoryAsync(string reason);
    }
}