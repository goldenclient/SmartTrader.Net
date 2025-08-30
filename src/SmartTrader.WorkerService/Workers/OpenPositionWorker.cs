// src/SmartTrader.WorkerService/Workers/OpenPositionWorker.cs
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using SmartTrader.Domain.Enums;
using SmartTrader.Infrastructure.Services;

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
                    var telegramNotifier = scope.ServiceProvider.GetRequiredService<ITelegramNotifier>(); // Resolve سرویس جدید


                    await telegramNotifier.SendNotificationHistoryAsync($"OpenPositionWorker running at: {DateTimeOffset.Now}");

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
                            var signal = await strategyHandler.GetSignalAsync(coin, strategy, signalExchange);

                            // 2. اگر سیگنالی وجود داشت، آن را روی تمام ولت‌های واجد شرایط اعمال کن
                            if (signal.Signal == SignalType.OpenLong || signal.Signal == SignalType.OpenShort)
                            {
                                await telegramNotifier.SendNotificationAsync(signal, coin.CoinName, strategy.StrategyName, "SIGNAL", 0);
                                _logger.LogInformation("Signal {Signal} for {CoinName} received. Notifying and applying to eligible wallets.", signal.Signal, coin.CoinName);
                                //await telegramNotifier.SendNotificationAsync(signal, coin.CoinName, strategy.StrategyName, "Strategy", 0);

                                //continue;
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
                                    if (await positionRepo.HasOpenPositionAsync(wallet.WalletID, symbol, strategy.StrategyID))
                                    {
                                        //signal.Reason = signal.Reason+"\nNot Open ==> Strategy HasOpen";
                                        //await telegramNotifier.SendNotificationAsync(signal, coin.CoinName, strategy.StrategyName, wallet.WalletName, 0);
                                        continue; // این ولت برای این کوین پوزیشن باز دارد
                                        // این ولت برای این استراتژی (فقط استراتژی هایی که onlyones=1 است)، پوزیشن باز دارد
                                    }

                                    // 3. اجرای معامله بر اساس پارامترهای سیگنال
                                    var exchangeService = exchangeFactory.CreateService(wallet, exchange);
                                    var filterInfo = await exchangeService.GetSymbolFilterInfoAsync(symbol);
                                    if (filterInfo == null)
                                    {
                                        _logger.LogWarning("Could not retrieve symbol filters for {Symbol}. Skipping trade.", symbol);
                                        continue;
                                    }
                                    var balance = await exchangeService.GetFreeBalanceAsync();
                                    var positionValue = balance * (signal.PercentBalance ?? 5.0m) / 100;
                                    var lastPrice = await exchangeService.GetLastPriceAsync(symbol);
                                    if (lastPrice == 0) continue;

                                    var initialQuantity = positionValue / lastPrice;
                                    initialQuantity = initialQuantity * (signal.Leverage ?? 1);
                                    // 3. اعتبارسنجی و تنظیم حجم معامله بر اساس قوانین صرافی
                                    if (initialQuantity < filterInfo.MinQuantity)
                                    {
                                        _logger.LogWarning("Calculated quantity {Quantity} is less than MinQuantity {MinQuantity} for {Symbol}. Skipping trade.", initialQuantity, filterInfo.MinQuantity, symbol);
                                        continue;
                                    }
                                    var adjustedQuantity = AdjustToStepSize(initialQuantity, filterInfo.StepSize);
                                    signal.Symbol = symbol;
                                    signal.Quantity = adjustedQuantity;
                                    // ارسال کل آبجکت سیگنال به سرویس صرافی
                                    var openResult = await exchangeService.OpenPositionAsync(signal);
                                    await telegramNotifier.SendNotificationAsync(signal, coin.CoinName, strategy.StrategyName, wallet.WalletName, lastPrice);
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
                                            OrderId = openResult.OrderId,
                                            Symbol = symbol,
                                            PositionSide = signal.Signal.ToString(),
                                            Status = PositionStatus.Open.ToString(), // استفاده از Enum
                                            EntryPrice = lastPrice,
                                            EntryValueUSD = positionValue,
                                            CurrentQuantity = openResult.Quantity,
                                            OpenTimestamp = DateTime.UtcNow,
                                            Stoploss = signal.StopLoss,
                                            TakeProfit = signal.TakeProfit,
                                            Leverage = strategy.Leverage ?? 1
                                        };
                                        await positionRepo.CreateAsync(newPosition);
                                        _logger.LogInformation("Position for {Symbol} on wallet {WalletName} opened.", symbol, wallet.WalletName);
                                    }
                                    else
                                        _logger.LogInformation("Position for {Symbol} on wallet {WalletName} Error:{WalletName}", symbol, wallet.WalletName,openResult.ErrorMessage);
                                }
                            }
                        }
                    }
                }


                await Task.Delay(TimeSpan.FromMinutes(_intervalMinutes), stoppingToken);
            }
        }

        private static decimal AdjustToStepSize(decimal quantity, decimal stepSize)
        {
            if (stepSize == 0)
            {
                return quantity;
            }
            // تعداد گام‌ها را محاسبه کرده و به پایین گرد می‌کنیم، سپس در اندازه گام ضرب می‌کنیم
            // این کار تضمین می‌کند که مقدار نهایی مضربی از stepSize و کوچکتر یا مساوی مقدار اولیه است
            return Math.Floor(quantity / stepSize) * stepSize;
        }

    }
}
