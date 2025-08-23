// src/SmartTrader.WorkerService/Workers/ClosePositionWorker.cs
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using SmartTrader.Domain.Enums;

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
                    if (openPositions.Any()) {
                        await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
                        continue; 
                    }

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

                        var strategyHandler = strategyFactory.CreateExitStrategy(exitStrategy);
                        var signal = await strategyHandler.ExecuteAsync(position);

                        if (signal.Signal != SignalType.Hold)
                        {
                            _logger.LogInformation("Executing action '{SignalType}' for position {PositionID}. Reason: {Reason}",
                                signal.Signal, position.PositionID, signal.Reason);

                            if (!wallets.TryGetValue(position.WalletID, out var wallet) || !exchanges.TryGetValue(wallet.ExchangeID, out var exchange))
                            {
                                _logger.LogError("Could not find wallet/exchange to execute trade for position {PositionID}.", position.PositionID);
                                continue;
                            }

                            var exchangeService = exchangeFactory.CreateService(wallet, exchange);
                            bool actionSuccess = false;
                            decimal actionPrice = await exchangeService.GetLastPriceAsync(position.Symbol);

                            switch (signal.Signal)
                            {
                                case SignalType.CloseByTP:
                                case SignalType.CloseBySL:
                                    var closeResult = await exchangeService.ClosePositionAsync(position.Symbol, position.PositionSide, position.CurrentQuantity);
                                    if (closeResult.IsSuccess)
                                    {
                                        position.Status = PositionStatus.Closed;
                                        position.ProfitUSD = (closeResult.AveragePrice - position.EntryPrice) * position.CurrentQuantity * (position.PositionSide == "LONG" ? 1 : -1);
                                        position.CloseTimestamp = DateTime.UtcNow;
                                        await positionRepo.UpdateAsync(position);
                                        actionSuccess = true;
                                    }
                                    break;

                                case SignalType.SellProfit:
                                    var quantityToSell = position.CurrentQuantity * (signal.PercentPosition.Value / 100);
                                    // TODO: Implement ModifyPositionAsync in IExchangeService and BinanceService
                                    // var sellResult = await exchangeService.ModifyPositionAsync(position.Symbol, "SELL", quantityToSell);
                                    // if(sellResult.IsSuccess) { ... update position quantity and realized PnL ...; actionSuccess = true; }
                                    break;

                                case SignalType.BuyRollback:
                                    var balance = await exchangeService.GetFreeBalanceAsync();
                                    var amountToBuyUSD = balance * (signal.PercentBalance.Value / 100);
                                    var quantityToBuy = amountToBuyUSD / actionPrice;
                                    // TODO: Implement ModifyPositionAsync in IExchangeService and BinanceService
                                    // var buyResult = await exchangeService.ModifyPositionAsync(position.Symbol, "BUY", quantityToBuy);
                                    // if(buyResult.IsSuccess) { ... update position quantity and average entry price ...; actionSuccess = true; }
                                    break;

                                case SignalType.ChangeSL:
                                    // TODO: Implement UpdateStopLossAsync in IExchangeService and BinanceService
                                    // var slResult = await exchangeService.UpdateStopLossAsync(position.Symbol, signal.NewStopLossPrice.Value);
                                    // if(slResult.IsSuccess) { actionSuccess = true; }
                                    break;
                            }

                            if (actionSuccess)
                            {
                                var history = new PositionHistory
                                {
                                    PositionID = position.PositionID,
                                    ActionType = (ActionType)Enum.Parse(typeof(ActionType), signal.Signal.ToString()), // Convert SignalType to ActionType
                                    PercentPosition = signal.PercentPosition,
                                    PercentBalance = signal.PercentBalance,
                                    Price = actionPrice,
                                    ActionTimestamp = DateTime.UtcNow,
                                    Description = signal.Reason
                                };
                                await positionRepo.AddHistoryAsync(history);
                                _logger.LogInformation("Action '{SignalType}' for position {PositionID} successfully executed and logged.", signal.Signal, position.PositionID);
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