// src/SmartTrader.Infrastructure/Services/BinanceService.cs
using Binance.Net.Clients;
using Binance.Net.Enums;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Models;
using System.Linq;
using System.Threading.Tasks;

//bingx:seure:   aZWxpP1yCbVVfJNYaIVQxqRZOdQwGJwXpSRJTrOEkmPJIhTZkOCjWMzKq0Zh0unwHfA0CcO4djsouajNctA
//bingx:api:     PwifrXu8sYfgFw0qnRpUQ0ndrHNlBSbenMG8IYuzayrfqMoYroRTrB77GRAKFHJUYhxQN5nYnZ0GTgKxN4jpUw
//binance secure: zRYFyQmIKCeNCKhJUIvYX31pTl5fS3LJNhuVHGdzmSoJ9haq1C960DBRbgTAVtpA
//binance api :   LFoqWEuTZpckOqoMTvVyj0tajAmPtdSAzGd0PpZeCh7P14ZTZHtKwvh0etdQszrL

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

        public async Task<IEnumerable<Kline>> GetKlinesAsync(string symbol)
        {
            //var result = await _client.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, KlineInterval.OneHour);
            var result = await _client.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, KlineInterval.OneHour,limit:300);
            if (!result.Success)
            {
                return [];
            }

            // تبدیل داده‌های بایننس به مدل مستقل ما
            return result.Data.Select(k => new Kline
            {
                OpenTime = k.OpenTime,
                OpenPrice = k.OpenPrice,
                HighPrice = k.HighPrice,
                LowPrice = k.LowPrice,
                ClosePrice = k.ClosePrice,
                Volume = k.Volume
            });
        }

        public async Task<SymbolFilterInfo> GetSymbolFilterInfoAsync(string symbol)
        {
            var exchangeInfo = await _client.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
            if (!exchangeInfo.Success) return null;

            var symbolInfo = exchangeInfo.Data.Symbols.FirstOrDefault(s => s.Name.Equals(symbol, StringComparison.OrdinalIgnoreCase));
            if (symbolInfo == null) return null;

            var lotSizeFilter = symbolInfo.LotSizeFilter;
            var priceFilter = symbolInfo.PriceFilter;

            return new SymbolFilterInfo
            {
                StepSize = lotSizeFilter?.StepSize ?? 0,
                MinQuantity = lotSizeFilter?.MinQuantity ?? 0,
                TickSize = priceFilter?.TickSize ?? 0
            };
        }
    }
}