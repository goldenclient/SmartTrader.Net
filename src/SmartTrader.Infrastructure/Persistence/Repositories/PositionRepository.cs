// src/SmartTrader.Infrastructure/Persistence/Repositories/PositionRepository.cs
using Dapper;
using Microsoft.Extensions.Configuration;
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Domain.Entities;
using SmartTrader.Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartTrader.Infrastructure.Persistence.Repositories
{
    public class PositionRepository : BaseRepository, IPositionRepository
    {
        public PositionRepository(IConfiguration configuration) : base(configuration) { }

        public async Task<Position> CreateAsync(Position position)
        {
            const string sql = @"
                INSERT INTO Positions (WalletID, CoinID, EntryStrategyID, ExitStrategyID, Symbol, PositionSide, Status, EntryPrice, EntryValueUSD, CurrentQuantity, OpenTimestamp)
                VALUES (@WalletID, @CoinID, @EntryStrategyID, @ExitStrategyID, @Symbol, @PositionSide, @Status, @EntryPrice, @EntryValueUSD, @CurrentQuantity, @OpenTimestamp);
                SELECT CAST(SCOPE_IDENTITY() as int)";

            using var connection = CreateConnection();
            var id = await connection.QuerySingleAsync<int>(sql, position);
            position.PositionID = id;
            return position;
        }

        public async Task<IEnumerable<Position>> GetOpenPositionsAsync()
        {
            // از نام Enum برای فیلتر کردن استفاده می‌کنیم
            string sql = "SELECT * FROM Positions WHERE Status = '" + PositionStatus.Open.ToString() + "'";
            using var connection = CreateConnection();
            return await connection.QueryAsync<Position>(sql);
        }

        public async Task<bool> HasOpenPositionAsync(int walletId, string symbol)
        {
            const string sql = "SELECT COUNT(1) FROM Positions WHERE WalletID = @WalletID AND Symbol = @Symbol AND Status = 'Open'";
            using var connection = CreateConnection();
            return await connection.ExecuteScalarAsync<bool>(sql, new { WalletID = walletId, Symbol = symbol });
        }

        public async Task UpdateAsync(Position position)
        {
            const string sql = @"
                UPDATE Positions 
                SET 
                    Status = @Status, 
                    ExitStrategyID = @ExitStrategyID,
                    CurrentQuantity = @CurrentQuantity,
                    ProfitUSD = @ProfitUSD,
                    CloseTimestamp = @CloseTimestamp
                WHERE PositionID = @PositionID";

            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, position);
        }

        // src/SmartTrader.Infrastructure/Persistence/Repositories/PositionRepository.cs
        public async Task<IEnumerable<PositionHistory>> GetHistoryByPositionIdAsync(int positionId)
        {
            const string sql = "SELECT * FROM PositionHistory WHERE PositionID = @PositionID ORDER BY ActionTimestamp";
            using var connection = CreateConnection();
            return await connection.QueryAsync<PositionHistory>(sql, new { PositionID = positionId });
        }

        public async Task AddHistoryAsync(PositionHistory history)
        {
            const string sql = @"
        INSERT INTO PositionHistory (PositionID, ActionType, PercentPosition, PercentBalance, Price, ActionTimestamp, Description)
        VALUES (@PositionID, @ActionType, @PercentPosition, @PercentBalance, @Price, @ActionTimestamp, @Description);";
            using var connection = CreateConnection();
            await connection.ExecuteAsync(sql, history);
        }

    }
}
