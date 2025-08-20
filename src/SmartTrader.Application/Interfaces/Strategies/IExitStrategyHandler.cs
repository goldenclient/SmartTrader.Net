using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities; // using جدید
using System.Threading.Tasks;

namespace SmartTrader.Application.Interfaces.Strategies
{
    public interface IExitStrategyHandler
    {
        // اکنون فقط پوزیشن را به عنوان ورودی می‌گیرد
        Task<StrategySignal> ExecuteAsync(Position position);
    }
}