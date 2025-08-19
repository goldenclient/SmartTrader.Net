// src/SmartTrader.Infrastructure/Services/ExchangeServiceFactory.cs
using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Domain.Entities;
using System;

namespace SmartTrader.Infrastructure.Services
{
    public class ExchangeServiceFactory : IExchangeServiceFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        // ILoggerFactory را برای ساختن لاگرها تزریق می‌کنیم
        public ExchangeServiceFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }
        public IExchangeService CreateService(Wallet wallet, Exchange exchange)
        {
            // بر اساس نام صرافی، سرویس مناسب را ایجاد و مقداردهی اولیه می‌کنیم
            switch (exchange.ExchangeName.ToLower())
            {
                case "binance":
                    // یک لاگر مخصوص برای BinanceService ساخته و به آن پاس می‌دهیم
                    return new BinanceService(wallet.ApiKey, wallet.SecretKey, _loggerFactory.CreateLogger<BinanceService>());

                case "bingx":
                    // یک لاگر مخصوص برای BingXService ساخته و به آن پاس می‌دهیم
                    return new BingXService(wallet.ApiKey, wallet.SecretKey, _loggerFactory.CreateLogger<BingXService>());

                default:
                    throw new NotSupportedException($"Exchange '{exchange.ExchangeName}' is not supported.");
            }
        }
    }
}
