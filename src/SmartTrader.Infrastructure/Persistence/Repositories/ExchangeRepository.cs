// src/SmartTrader.Infrastructure/Persistence/Repositories/ExchangeRepository.cs
using Dapper;
using Microsoft.Extensions.Configuration;
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartTrader.Infrastructure.Persistence.Repositories
{
    public class ExchangeRepository : BaseRepository, IExchangeRepository
    {
        public ExchangeRepository(IConfiguration configuration) : base(configuration) { }

        public async Task<IEnumerable<Exchange>> GetAllAsync()
        {
            const string sql = "SELECT * FROM Exchanges";
            using var connection = CreateConnection();
            return await connection.QueryAsync<Exchange>(sql);
        }
    }
}