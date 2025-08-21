// src/SmartTrader.Application/Interfaces/Services/IExchangeService.cs
using SmartTrader.Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;
using SmartTrader.Application.Models; // این using را اضافه کنید

namespace SmartTrader.Application.Interfaces.Services
{
    // این مدل برای بازگرداندن نتیجه یک سفارش استفاده می‌شود
    public class OrderResult
    {
        public bool IsSuccess { get; set; }
        public long OrderId { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal Quantity { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public interface IExchangeService
    {
        // متد Initialize حذف شد
        Task<decimal> GetFreeBalanceAsync(string asset = "USDT");
        Task<decimal> GetLastPriceAsync(string symbol);
        Task<OrderResult> OpenPositionAsync(StrategySignal signal);
        Task<OrderResult> ClosePositionAsync(string symbol, string side, decimal quantity);
        Task<IEnumerable<Kline>> GetKlinesAsync(string symbol); // متد جدید
        Task<SymbolFilterInfo> GetSymbolFilterInfoAsync(string symbol); // متد جدید
    }
}
