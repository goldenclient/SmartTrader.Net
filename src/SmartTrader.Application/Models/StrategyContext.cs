// src/SmartTrader.Application/Models/StrategyContext.cs
using SmartTrader.Domain.Entities;
using System.Collections.Generic;


namespace SmartTrader.Application.Models
{
    public class StrategyContext
    {
        // اطلاعات اصلی
        public Position Position { get; set; } // برای استراتژی ورود می‌تواند null باشد
        public Wallet Wallet { get; set; }
        public Strategy Strategy { get; set; }

        // اطلاعات لحظه‌ای بازار
        public decimal CurrentPrice { get; set; }
        public IEnumerable<Kline>? Klines { get; set; }

        // اطلاعات محاسبه شده
        public decimal? CurrentPnlPercentage { get; set; }
        public decimal WalletFreeBalance { get; set; }
        public double? Rsi { get; set; }
        public string? Symbol { get; set; }
        // ... سایر اندیکاتورها

        // اطلاعات تاریخی
        public IEnumerable<PositionHistory> History { get; set; }
    }
}