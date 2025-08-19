// src/SmartTrader.WorkerService/Workers/OpenPositionWorker.cs
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;

namespace SmartTrader.WorkerService.Workers
{
    public class OpenPositionWorker : BackgroundService
    {
        private readonly ILogger<OpenPositionWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly int _intervalMinutes;

        public OpenPositionWorker(ILogger<OpenPositionWorker> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _intervalMinutes = configuration.GetValue<int>("WorkerSettings:OpenPositionWorkerIntervalMinutes", 60);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("OpenPositionWorker running at: {time}", DateTimeOffset.Now);

                using (var scope = _serviceProvider.CreateScope())
                {
                    // Resolve services from the scope
                    var positionRepo = scope.ServiceProvider.GetRequiredService<IPositionRepository>();
                    var walletRepo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
                    var strategyRepo = scope.ServiceProvider.GetRequiredService<IStrategyRepository>();
                    var exchangeRepo = scope.ServiceProvider.GetRequiredService<IExchangeRepository>();
                    // CoinRepository را هم اضافه کنید
                    var exchangeFactory = scope.ServiceProvider.GetRequiredService<IExchangeServiceFactory>();
                    var strategyFactory = scope.ServiceProvider.GetRequiredService<IStrategyFactory>();

                    // Fetch all necessary data once
                    var wallets = await walletRepo.GetActiveWalletsAsync();
                    var exchanges = (await exchangeRepo.GetAllAsync()).ToDictionary(e => e.ExchangeID);
                    var allStrategies = (await strategyRepo.GetAllAsync()).ToDictionary(s => s.StrategyID);
                    var entryStrategies = allStrategies.Values.Where(s => s.StrategyType == "Entry" && s.IsActive);

                    foreach (var strategy in entryStrategies)
                    {
                        var tradableCoins = await strategyRepo.GetTradableCoinsByStrategyIdAsync(strategy.StrategyID);
                        // شما به یک CoinRepository برای دریافت اطلاعات کامل کوین‌ها نیاز دارید.
                        // var allCoins = (await coinRepo.GetAllAsync()).ToDictionary(c => c.CoinID);

                        foreach (var tradableCoin in tradableCoins)
                        {
                            // var coin = allCoins[tradableCoin.CoinID];
                            // string symbol = coin.Symbol;
                            string symbol = "BTCUSDT"; // مثال

                            foreach (var wallet in wallets)
                            {
                                if (await positionRepo.HasOpenPositionAsync(wallet.WalletID, symbol))
                                {
                                    _logger.LogInformation("Skipping {Symbol} for wallet {WalletName}, position already open.", symbol, wallet.WalletName);
                                    continue;
                                }

                                if (!exchanges.TryGetValue(wallet.ExchangeID, out var exchange)) continue;

                                // 1. Create exchange service
                                var exchangeService = exchangeFactory.CreateService(wallet, exchange);

                                // ۲. ساخت آبجکت Context برای استراتژی ورود
                                var context = new StrategyContext
                                {
                                    Position = null, // پوزیشنی وجود ندارد
                                    Wallet = wallet,
                                    Strategy = strategy,
                                    Symbol = symbol, // پراپرتی جدید در StrategyContext
                                                     // Klines = await exchangeService.GetKlinesAsync(symbol),
                                                     // Rsi = IndicatorCalculator.CalculateRsi(...)
                                };

                                // ۳. ساخت و اجرای استراتژی
                                var strategyHandler = strategyFactory.CreateStrategy(strategy, exchangeService);
                                var signal = await strategyHandler.ExecuteAsync(context);


                                if (signal.Signal == SignalType.OpenLong || signal.Signal == SignalType.OpenShort)
                                {
                                    _logger.LogInformation("Signal {Signal} found for {Symbol} on wallet {WalletName}", signal.Signal, symbol, wallet.WalletName);

                                    // 4. Calculate position size and open position
                                    var balance = await exchangeService.GetFreeBalanceAsync();
                                    var positionValue = balance * (strategy.BalancePercentToTrade ?? wallet.MaxBalancePercentToTrade) / 100;
                                    var lastPrice = await exchangeService.GetLastPriceAsync(symbol);
                                    if (lastPrice == 0) continue;

                                    var quantity = positionValue / lastPrice;
                                    // TODO: مقدار quantity را بر اساس قوانین صرافی (precision, min quantity) رند کنید.

                                    var positionSide = signal.Signal == SignalType.OpenLong ? "LONG" : "SHORT";
                                    var openResult = await exchangeService.OpenPositionAsync(symbol, positionSide, quantity);

                                    if (openResult.IsSuccess)
                                    {
                                        var newPosition = new Position
                                        {
                                            WalletID = wallet.WalletID,
                                            CoinID = tradableCoin.CoinID,
                                            EntryStrategyID = strategy.StrategyID,
                                            // ExitStrategyID را بر اساس منطق خودتان تنظیم کنید
                                            Symbol = symbol,
                                            PositionSide = positionSide,
                                            Status = "OPEN",
                                            EntryPrice = openResult.AveragePrice,
                                            EntryValueUSD = positionValue,
                                            CurrentQuantity = openResult.Quantity,
                                            OpenTimestamp = DateTime.UtcNow
                                        };
                                        await positionRepo.CreateAsync(newPosition);
                                        _logger.LogInformation("Position successfully opened for {Symbol} and saved to DB.", symbol);
                                    }
                                    else
                                    {
                                        _logger.LogError("Failed to open position for {Symbol}: {Error}", symbol, openResult.ErrorMessage);
                                    }
                                }
                            }
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);
            }
        }
    }
}
