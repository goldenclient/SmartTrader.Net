// src/SmartTrader.Infrastructure/Services/BinanceService.cs
using Binance.Net.Clients;
using Binance.Net.Enums;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Services;
using System.Linq;
using System.Threading.Tasks;

namespace SmartTrader.Infrastructure.Services
{
    public class BinanceService : IExchangeService
    {

        private readonly BinanceRestClient _client;
        private readonly ILogger<BinanceService> _logger;

        // کلیدها و لاگر از طریق سازنده دریافت می‌شوند
        public BinanceService(string apiKey, string secretKey, ILogger<BinanceService> logger)
        {
            _logger = logger;
            _client = new BinanceRestClient(options =>
            {
                options.ApiCredentials = new ApiCredentials(apiKey, secretKey);
            });
        }

        public async Task<decimal> GetFreeBalanceAsync(string asset = "USDT")
        {
            var accountInfo = await _client.UsdFuturesApi.Account.GetAccountInfoV2Async();
            if (!accountInfo.Success) return 0;
            return accountInfo.Data.Assets.FirstOrDefault(a => a.Asset == asset)?.AvailableBalance ?? 0;
        }

        public async Task<decimal> GetLastPriceAsync(string symbol)
        {
            var ticker = await _client.UsdFuturesApi.ExchangeData.GetTickerAsync(symbol);
            return ticker.Success ? ticker.Data.LastPrice : 0;
        }

        public async Task<OrderResult> OpenPositionAsync(string symbol, string side, decimal quantity)
        {
            var orderSide = side.ToUpper() == "LONG" ? OrderSide.Buy : OrderSide.Sell;
            var result = await _client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, orderSide, FuturesOrderType.Market, quantity);

            return result.Success
                ? new OrderResult { IsSuccess = true, OrderId = result.Data.Id, AveragePrice = result.Data.AveragePrice, Quantity = result.Data.Quantity }
                : new OrderResult { IsSuccess = false, ErrorMessage = result.Error?.Message };
        }

        public async Task<OrderResult> ClosePositionAsync(string symbol, string side, decimal quantity)
        {
            var closeSide = side.ToUpper() == "LONG" ? OrderSide.Sell : OrderSide.Buy;
            var result = await _client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, closeSide, FuturesOrderType.Market, quantity, reduceOnly: true);

            return result.Success
                ? new OrderResult { IsSuccess = true, OrderId = result.Data.Id, AveragePrice = result.Data.AveragePrice, Quantity = result.Data.Quantity }
                : new OrderResult { IsSuccess = false, ErrorMessage = result.Error?.Message };
        }
    }
}