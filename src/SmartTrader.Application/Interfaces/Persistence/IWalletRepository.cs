using SmartTrader.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Persistence
{
    public interface IWalletRepository
    {
        Task<IEnumerable<Wallet>> GetActiveWalletsAsync();
    }
}
