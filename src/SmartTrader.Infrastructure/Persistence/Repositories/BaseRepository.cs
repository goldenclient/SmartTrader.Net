// src/SmartTrader.Infrastructure/Persistence/Repositories/BaseRepository.cs
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SmartTrader.Infrastructure.Persistence.Repositories
{
    public abstract class BaseRepository
    {
        private readonly string _connectionString;

        protected BaseRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        protected SqlConnection CreateConnection() => new SqlConnection(_connectionString);
    }
}
