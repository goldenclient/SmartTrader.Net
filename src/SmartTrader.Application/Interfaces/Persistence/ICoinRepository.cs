// src/SmartTrader.Application/Interfaces/Persistence/ICoinRepository.cs (جدید)
using SmartTrader.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Persistence
{
    public interface ICoinRepository
    {
        Task<IEnumerable<Coin>> GetAllAsync();
    }
}