// src/SmartTrader.Application/Interfaces/Services/IExchangeServiceFactory.cs
using SmartTrader.Domain.Entities;

namespace SmartTrader.Application.Interfaces.Services
{
    public interface IExchangeServiceFactory
    {
        IExchangeService CreateService(Wallet wallet, Exchange exchange);
    }
}