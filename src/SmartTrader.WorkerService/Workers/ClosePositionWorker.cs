// src/SmartTrader.WorkerService/Workers/ClosePositionWorker.cs
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;

namespace SmartTrader.WorkerService.Workers
{
    public class ClosePositionWorker : BackgroundService
    {
        private readonly ILogger<ClosePositionWorker> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly int _intervalSeconds;

        public ClosePositionWorker(ILogger<ClosePositionWorker> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _intervalSeconds = configuration.GetValue<int>("WorkerSettings:ClosePositionWorkerIntervalSeconds", 30);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("ClosePositionWorker running at: {time}", DateTimeOffset.Now);

                using (var scope = _serviceProvider.CreateScope())
                {
                    // Resolve services from the scope
                    var positionRepo = scope.ServiceProvider.GetRequiredService<IPositionRepository>();
                    var walletRepo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
                    var strategyRepo = scope.ServiceProvider.GetRequiredService<IStrategyRepository>();
                    var exchangeRepo = scope.ServiceProvider.GetRequiredService<IExchangeRepository>();
                    var exchangeFactory = scope.ServiceProvider.GetRequiredService<IExchangeServiceFactory>();
                    var strategyFactory = scope.ServiceProvider.GetRequiredService<IStrategyFactory>();

                    // Fetch all necessary data once and store in dictionaries for fast lookup
                    var openPositions = await positionRepo.GetOpenPositionsAsync();
                    if (!openPositions.Any()) continue;

                    var wallets = (await walletRepo.GetActiveWalletsAsync()).ToDictionary(w => w.WalletID);
                    var exchanges = (await exchangeRepo.GetAllAsync()).ToDictionary(e => e.ExchangeID);
                    var strategies = (await strategyRepo.GetAllAsync()).ToDictionary(s => s.StrategyID);

                    foreach (var position in openPositions)
                    {
                        // Find the related entities for the current position from the dictionaries
                        if (!wallets.TryGetValue(position.WalletID, out var wallet))
                        {
                            _logger.LogWarning("Wallet with ID {WalletID} for position {PositionID} not found or is inactive.", position.WalletID, position.PositionID);
                            continue;
                        }

                        if (!exchanges.TryGetValue(wallet.ExchangeID, out var exchange))
                        {
                            _logger.LogWarning("Exchange with ID {ExchangeID} for wallet {WalletID} not found.", wallet.ExchangeID, wallet.WalletID);
                            continue;
                        }

                        if (!position.ExitStrategyID.HasValue || !strategies.TryGetValue(position.ExitStrategyID.Value, out var exitStrategy))
                        {
                            _logger.LogWarning("Exit strategy for position {PositionID} is not defined or not found.", position.PositionID);
                            continue;
                        }

                        // 1. Create the correct exchange service using the factory
                        var exchangeService = exchangeFactory.CreateService(wallet, exchange);

                        // 2. Create the correct strategy handler using the factory
                        var strategyHandler = strategyFactory.CreateStrategy(exitStrategy, exchangeService);

                        // 3. Execute the strategy to get a signal
                        var signal = await strategyHandler.ExecuteAsync(position, wallet, exitStrategy);

                        // 4. Act based on the signal
                        if (signal.Signal == SignalType.Close)
                        {
                            _logger.LogInformation("Closing position {PositionID} due to signal: {Reason}", position.PositionID, signal.Reason);
                            var closeResult = await exchangeService.ClosePositionAsync(position.Symbol, position.PositionSide, position.CurrentQuantity);

                            if (closeResult.IsSuccess)
                            {
                                // Update the position in the database
                                position.Status = "CLOSED";
                                position.ProfitUSD = (closeResult.AveragePrice - position.EntryPrice) * position.CurrentQuantity * (position.PositionSide == "LONG" ? 1 : -1);
                                position.CloseTimestamp = DateTime.UtcNow;
                                await positionRepo.UpdateAsync(position);
                                _logger.LogInformation("Position {PositionID} successfully closed in the database.", position.PositionID);
                            }
                            else
                            {
                                _logger.LogError("Failed to close position {PositionID} on the exchange: {Error}", position.PositionID, closeResult.ErrorMessage);
                            }
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
        }
    }
}
//foreach (var position in openPositions)
//                    {
//                        // TODO: منطق مدیریت پوزیشن‌های باز در اینجا پیاده‌سازی می‌شود.
//                        // 1. اطلاعات کیف پول مربوط به پوزیشن را دریافت کنید تا به کلیدهای API دسترسی داشته باشید.
//                        // 2. exchangeService را با کلیدهای API مربوطه Initialize کنید.
//                        // 3. قیمت لحظه‌ای کوین را با exchangeService.GetLastPriceAsync دریافت کنید.
//                        // 4. سود/زیان را محاسبه کنید.
//                        // 5. بر اساس استراتژی خروج (حد سود/ضرر)، تصمیم به بستن پوزیشن بگیرید.
//                        // 6. در صورت نیاز، پوزیشن را با exchangeService.ClosePositionAsync ببندید.
//                        // 7. وضعیت پوزیشن را در دیتابیس با positionRepository.UpdateAsync به‌روزرسانی کنید.
//                    }