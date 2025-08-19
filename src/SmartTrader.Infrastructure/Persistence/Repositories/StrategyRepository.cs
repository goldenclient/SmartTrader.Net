// src/SmartTrader.Infrastructure/Persistence/Repositories/StrategyRepository.cs
using Dapper;
using Microsoft.Extensions.Configuration;
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartTrader.Infrastructure.Persistence.Repositories
{
    public class StrategyRepository : BaseRepository, IStrategyRepository
    {
        public StrategyRepository(IConfiguration configuration) : base(configuration) { }

        public async Task<IEnumerable<StrategyTradableCoin>> GetTradableCoinsByStrategyIdAsync(int strategyId)
        {
            const string sql = "SELECT * FROM StrategyTradableCoins WHERE StrategyID = @StrategyID AND IsActive = 1";
            using var connection = CreateConnection();
            return await connection.QueryAsync<StrategyTradableCoin>(sql, new { StrategyID = strategyId });
        }

        public async Task<IEnumerable<Strategy>> GetAllAsync()
        {
            const string sql = "SELECT * FROM Strategies WHERE IsActive = 1";
            using var connection = CreateConnection();
            return await connection.QueryAsync<Strategy>(sql);
        }

    }
}