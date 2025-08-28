// src/SmartTrader.Application/Interfaces/Persistence/IPositionRepository.cs
using SmartTrader.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Persistence
{
    public interface IPositionRepository
    {
        Task<bool> HasOpenPositionAsync(int walletId, string symbol, int entrySignalId);
        Task<Position> CreateAsync(Position position);
        Task UpdateAsync(Position position);
        Task<IEnumerable<Position>> GetOpenPositionsAsync();
        Task<IEnumerable<PositionHistory>> GetHistoryByPositionIdAsync(int positionId);
        Task AddHistoryAsync(PositionHistory history);
    }
}