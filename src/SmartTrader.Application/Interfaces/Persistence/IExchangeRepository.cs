// src/SmartTrader.Application/Interfaces/Persistence/IExchangeRepository.cs
using SmartTrader.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Persistence
{
    public interface IExchangeRepository
    {
        Task<IEnumerable<Exchange>> GetAllAsync();
    }
}
