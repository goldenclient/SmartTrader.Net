// src/SmartTrader.Infrastructure/Persistence/Repositories/WalletRepository.cs
using Dapper;
using Microsoft.Extensions.Configuration;
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartTrader.Infrastructure.Persistence.Repositories
{
    public class WalletRepository : BaseRepository, IWalletRepository
    {
        public WalletRepository(IConfiguration configuration) : base(configuration) { }

        public async Task<IEnumerable<Wallet>> GetActiveWalletsAsync()
        {
            const string sql = "SELECT * FROM Wallets WHERE IsActive = 1";
            using var connection = CreateConnection();
            return await connection.QueryAsync<Wallet>(sql);
        }
    }
}