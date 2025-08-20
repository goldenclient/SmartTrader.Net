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
                    var positionRepo = scope.ServiceProvider.GetRequiredService<IPositionRepository>();
                    var strategyRepo = scope.ServiceProvider.GetRequiredService<IStrategyRepository>();
                    var strategyFactory = scope.ServiceProvider.GetRequiredService<IStrategyFactory>();
                    var walletRepo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
                    var exchangeRepo = scope.ServiceProvider.GetRequiredService<IExchangeRepository>();
                    var exchangeFactory = scope.ServiceProvider.GetRequiredService<IExchangeServiceFactory>();

                    var openPositions = await positionRepo.GetOpenPositionsAsync();
                    if (!openPositions.Any()) continue;

                    var strategies = (await strategyRepo.GetAllAsync()).ToDictionary(s => s.StrategyID);
                    var wallets = (await walletRepo.GetActiveWalletsAsync()).ToDictionary(w => w.WalletID);
                    var exchanges = (await exchangeRepo.GetAllAsync()).ToDictionary(e => e.ExchangeID);

                    foreach (var position in openPositions)
                    {
                        if (!position.ExitStrategyID.HasValue || !strategies.TryGetValue(position.ExitStrategyID.Value, out var exitStrategy))
                        {
                            _logger.LogWarning("Exit strategy for position {PositionID} is not defined or found.", position.PositionID);
                            continue;
                        }

                        // 1. ساخت هندلر استراتژی
                        var strategyHandler = strategyFactory.CreateExitStrategy(exitStrategy);

                        // 2. اجرای استراتژی فقط با ارسال پوزیشن
                        var signal = await strategyHandler.ExecuteAsync(position);

                        // 3. اقدام بر اساس سیگنال دریافتی
                        if (signal.Signal == SignalType.Close)
                        {
                            _logger.LogInformation("Closing position {PositionID} due to signal: {Reason}", position.PositionID, signal.Reason);

                            if (!wallets.TryGetValue(position.WalletID, out var wallet) || !exchanges.TryGetValue(wallet.ExchangeID, out var exchange))
                            {
                                _logger.LogError("Could not find wallet/exchange to execute closing trade for position {PositionID}.", position.PositionID);
                                continue;
                            }

                            var exchangeService = exchangeFactory.CreateService(wallet, exchange);
                            var closeResult = await exchangeService.ClosePositionAsync(position.Symbol, position.PositionSide, position.CurrentQuantity);

                            if (closeResult.IsSuccess)
                            {
                                // ... (آپدیت پوزیشن در دیتابیس)
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