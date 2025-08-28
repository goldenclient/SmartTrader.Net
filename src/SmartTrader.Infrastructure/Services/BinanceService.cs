// src/SmartTrader.Infrastructure/Services/BinanceService.cs
using Binance.Net.Clients;
using Binance.Net.Enums;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using SmartTrader.Domain.Enums;
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

        public async Task<OrderResult> OpenPositionAsync(StrategySignal signal)
        {
            try
            {
                // 1. تنظیم نوع مارجین به ISOLATED
                var marginTypeResult = await _client.UsdFuturesApi.Account.ChangeMarginTypeAsync(signal.Symbol, FuturesMarginType.Isolated);
                if (!marginTypeResult.Success)
                {
                    // این خطا ممکن است در صورتی که پوزیشن باز وجود داشته باشد رخ دهد، پس فقط لاگ می‌کنیم
                    _logger.LogWarning("Could not set margin type to ISOLATED for {Symbol}: {Error}", signal.Symbol, marginTypeResult.Error?.Message);
                }

                // 2. تنظیم لوریج
                if (signal.Leverage.HasValue)
                {
                    var leverageResult = await _client.UsdFuturesApi.Account.ChangeInitialLeverageAsync(signal.Symbol, signal.Leverage.Value);
                    if (!leverageResult.Success)
                    {
                        _logger.LogError("Failed to set leverage to {Leverage} for {Symbol}: {Error}", signal.Leverage.Value, signal.Symbol, leverageResult.Error?.Message);
                        return new OrderResult { IsSuccess = false, ErrorMessage = $"Failed to set leverage: {leverageResult.Error?.Message}" };
                    }
                }

                // 3. باز کردن پوزیشن
                var orderSide = signal.Signal == SignalType.OpenLong ? OrderSide.Buy : OrderSide.Sell;
                var result = await _client.UsdFuturesApi.Trading.PlaceOrderAsync(signal.Symbol, orderSide, FuturesOrderType.Market, signal.Quantity);

                return result.Success
                    ? new OrderResult { IsSuccess = true, OrderId = result.Data.Id, AveragePrice = result.Data.AveragePrice, Quantity = result.Data.Quantity }
                    : new OrderResult { IsSuccess = false, ErrorMessage = result.Error?.Message };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while opening position for {Symbol}.", signal.Symbol);
                return new OrderResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<OrderResult> ClosePositionAsync(string symbol, string side, decimal quantity)
        {
            var closeSide = side.ToUpper() == SignalType.OpenLong.ToString().ToUpper() ? OrderSide.Sell : OrderSide.Buy;
            var result = await _client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, closeSide, FuturesOrderType.Market, quantity, reduceOnly: true);

            return result.Success
                ? new OrderResult { IsSuccess = true, OrderId = result.Data.Id, AveragePrice = result.Data.AveragePrice, Quantity = result.Data.Quantity }
                : new OrderResult { IsSuccess = false, ErrorMessage = result.Error?.Message };
        }

        public async Task<IEnumerable<Kline>> GetKlinesAsync(string symbol,string timeframe, int limit)
        {
            var tm = KlineInterval.FifteenMinutes;
            switch (timeframe)
            {
                case "5":
                    tm= KlineInterval.FiveMinutes;
                    break;
                case "30":
                    tm = KlineInterval.ThirtyMinutes;
                    break;
                case "60":
                    tm = KlineInterval.OneHour;
                    break;
                case "240":
                    tm = KlineInterval.FourHour;
                    break;
                default:
                    tm = KlineInterval.FifteenMinutes;
                    break;
            }

            var result = await _client.UsdFuturesApi.ExchangeData.GetKlinesAsync(symbol, tm,limit:limit);
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

        // src/SmartTrader.Infrastructure/Services/BinanceService.cs
        public async Task<OrderResult> ModifyPositionAsync(string symbol, string side, decimal quantity)
        {
            var orderSide = side.ToUpper() == SignalType.OpenLong.ToString().ToUpper() ? OrderSide.Buy : OrderSide.Sell;
            // هنگام فروش بخشی، سفارش باید از نوع reduceOnly باشد
            // هنگام خرید مجدد، سفارش از نوع عادی است
            bool reduceOnly = orderSide == OrderSide.Sell;

            var result = await _client.UsdFuturesApi.Trading.PlaceOrderAsync(symbol, orderSide, FuturesOrderType.Market, quantity, reduceOnly: reduceOnly);

            return result.Success
                ? new OrderResult { IsSuccess = true, OrderId = result.Data.Id, AveragePrice = result.Data.AveragePrice, Quantity = result.Data.Quantity }
                : new OrderResult { IsSuccess = false, ErrorMessage = result.Error?.Message };
        }

        public async Task<bool> UpdateStopLossAsync(string symbol, string positionSide, decimal stopPrice)
        {
            // ابتدا تمام سفارش‌های باز (از جمله SL/TP قبلی) را لغو می‌کنیم
            var cancelResult = await _client.UsdFuturesApi.Trading.CancelAllOrdersAsync(symbol);
            if (!cancelResult.Success)
            {
                _logger.LogWarning("Could not cancel existing orders for {symbol} before placing new SL. It might proceed anyway.", symbol);
            }

            // برای تنظیم حد ضرر، یک سفارش STOP_MARKET در جهت مخالف پوزیشن ثبت می‌کنیم
            var orderSide = positionSide.ToUpper() == SignalType.OpenLong.ToString().ToUpper() ? OrderSide.Sell : OrderSide.Buy;

            var result = await _client.UsdFuturesApi.Trading.PlaceOrderAsync(
                symbol,
                orderSide,
                FuturesOrderType.StopMarket,
                quantity: null, // مقدار لازم نیست چون کل پوزیشن بسته می‌شود
                positionSide: null,
                timeInForce: null,
                reduceOnly: true, // این پارامتر برای سفارش‌های SL/TP ضروری است
                price: null,
                stopPrice: stopPrice, // قیمت فعال‌سازی حد ضرر
                closePosition: true // تضمین می‌کند که کل پوزیشن بسته شود
            );

            if (!result.Success)
            {
                _logger.LogError("Failed to place Stop Loss order for {symbol}: {error}", symbol, result.Error?.Message);
                return false;
            }

            _logger.LogInformation("Successfully placed new Stop Loss order for {symbol} at {price}", symbol, stopPrice);
            return true;
        }
    }
}