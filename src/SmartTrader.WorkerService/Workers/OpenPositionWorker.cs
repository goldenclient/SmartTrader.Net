// src/SmartTrader.WorkerService/Workers/OpenPositionWorker.cs
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using SmartTrader.Domain.Enums;

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
                    var positionRepo = scope.ServiceProvider.GetRequiredService<IPositionRepository>();
                    var walletRepo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
                    var strategyRepo = scope.ServiceProvider.GetRequiredService<IStrategyRepository>();
                    var exchangeRepo = scope.ServiceProvider.GetRequiredService<IExchangeRepository>();
                    var coinRepo = scope.ServiceProvider.GetRequiredService<ICoinRepository>(); // فرض وجود
                    var exchangeFactory = scope.ServiceProvider.GetRequiredService<IExchangeServiceFactory>();
                    var strategyFactory = scope.ServiceProvider.GetRequiredService<IStrategyFactory>();

                    var allStrategies = (await strategyRepo.GetAllAsync()).ToDictionary(s => s.StrategyID);
                    var entryStrategies = allStrategies.Values.Where(s => s.IsEntryStrategy && s.IsActive);
                    var defaultExitStrategy = allStrategies.Values.FirstOrDefault(s => !s.IsEntryStrategy && s.IsActive);
                    var wallets = await walletRepo.GetActiveWalletsAsync();
                    var exchanges = (await exchangeRepo.GetAllAsync()).ToDictionary(e => e.ExchangeID);
                    var allCoins = (await coinRepo.GetAllAsync()).ToDictionary(c => c.CoinID);

                    foreach (var strategy in entryStrategies)
                    {
                        var tradableCoins = await strategyRepo.GetTradableCoinsByStrategyIdAsync(strategy.StrategyID);
                        var strategyHandler = strategyFactory.CreateEntryStrategy(strategy);

                        foreach (var tradableCoin in tradableCoins)
                        {
                            if (!allCoins.TryGetValue(tradableCoin.CoinID, out var coin)) continue;

                            // فرض می‌کنیم سیگنال را از صرافی بایننس می‌گیریم
                            string signalExchange = "binance";

                            // 1. دریافت سیگنال یک بار برای هر کوین
                            var signal = await strategyHandler.GetSignalAsync(coin, signalExchange);

                            // 2. اگر سیگنالی وجود داشت، آن را روی تمام ولت‌های واجد شرایط اعمال کن
                            if (signal.Signal == SignalType.OpenLong || signal.Signal == SignalType.OpenShort)
                            {
                                _logger.LogInformation("Signal {Signal} for {CoinName} received. Applying to eligible wallets.", signal.Signal, coin.CoinName);

                                foreach (var wallet in wallets)
                                {
                                    if (!exchanges.TryGetValue(wallet.ExchangeID, out var exchange) ||
                                        !exchange.ExchangeName.Equals(signalExchange, StringComparison.OrdinalIgnoreCase))
                                    {
                                        continue; // این ولت مربوط به صرافی مورد نظر نیست
                                    }

                                    var exchangeInfo = coin.GetExchangeInfo().FirstOrDefault(e => e.Exchange.Equals(exchange.ExchangeName, StringComparison.OrdinalIgnoreCase));
                                    if (exchangeInfo == null) continue;

                                    string symbol = exchangeInfo.Symbol;
                                    if (await positionRepo.HasOpenPositionAsync(wallet.WalletID, symbol))
                                    {
                                        continue; // این ولت برای این کوین پوزیشن باز دارد
                                    }

                                    // 3. اجرای معامله بر اساس پارامترهای سیگنال
                                    var exchangeService = exchangeFactory.CreateService(wallet, exchange);
                                    var balance = await exchangeService.GetFreeBalanceAsync();
                                    var positionValue = balance * (signal.PercentBalance ?? 5.0m) / 100;
                                    var lastPrice = await exchangeService.GetLastPriceAsync(symbol);
                                    if (lastPrice == 0) continue;

                                    var quantity = positionValue / lastPrice;
                                    // TODO: رند کردن مقدار quantity بر اساس lot size

                                    var positionSide = signal.Signal == SignalType.OpenLong ? "LONG" : "SHORT";
                                    var openResult = await exchangeService.OpenPositionAsync(symbol, positionSide, quantity);

                                    if (openResult.IsSuccess)
                                    {
                                        // انتخاب استراتژی خروج
                                        int? exitStrategyId = wallet.ForceExitStrategyID ?? defaultExitStrategy?.StrategyID;
                                        if (!exitStrategyId.HasValue)
                                        {
                                            _logger.LogWarning("No exit strategy could be assigned for new position on wallet {WalletName}.", wallet.WalletName);
                                        }
                                        var newPosition = new Position
                                        {
                                            WalletID = wallet.WalletID,
                                            CoinID = coin.CoinID,
                                            EntryStrategyID = strategy.StrategyID,
                                            ExitStrategyID = exitStrategyId, // منطق جدید
                                            Symbol = symbol,
                                            PositionSide = positionSide,
                                            Status = PositionStatus.Open, // استفاده از Enum
                                            EntryPrice = openResult.AveragePrice,
                                            EntryValueUSD = positionValue,
                                            CurrentQuantity = openResult.Quantity,
                                            OpenTimestamp = DateTime.UtcNow
                                        };
                                        await positionRepo.CreateAsync(newPosition);
                                        _logger.LogInformation("Position for {Symbol} on wallet {WalletName} opened.", symbol, wallet.WalletName);
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
