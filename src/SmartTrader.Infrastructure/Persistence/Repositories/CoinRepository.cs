// src/SmartTrader.Infrastructure/Persistence/Repositories/CoinRepository.cs (جدید)
using Dapper;
using Microsoft.Extensions.Configuration;
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartTrader.Infrastructure.Persistence.Repositories
{
    public class CoinRepository : BaseRepository, ICoinRepository
    {
        public CoinRepository(IConfiguration configuration) : base(configuration) { }

        public async Task<IEnumerable<Coin>> GetAllAsync()
        {
            const string sql = "SELECT * FROM Coins";
            using var connection = CreateConnection();
            return await connection.QueryAsync<Coin>(sql);
        }
    }
}