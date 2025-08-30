using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
using SmartTrader.Domain.Enums;
using SmartTrader.Infrastructure.Services;

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

                using var scope = _serviceProvider.CreateScope();

                var positionRepo = scope.ServiceProvider.GetRequiredService<IPositionRepository>();
                var strategyRepo = scope.ServiceProvider.GetRequiredService<IStrategyRepository>();
                var strategyFactory = scope.ServiceProvider.GetRequiredService<IStrategyFactory>();
                var walletRepo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
                var exchangeRepo = scope.ServiceProvider.GetRequiredService<IExchangeRepository>();
                var exchangeFactory = scope.ServiceProvider.GetRequiredService<IExchangeServiceFactory>();
                var telegramNotifier = scope.ServiceProvider.GetRequiredService<ITelegramNotifier>(); // Resolve سرویس جدید

                var openPositions = await positionRepo.GetOpenPositionsAsync();
                if (!openPositions.Any())
                {
                    await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
                    continue;
                }

                var strategies = (await strategyRepo.GetAllAsync()).ToDictionary(s => s.StrategyID);
                var wallets = (await walletRepo.GetActiveWalletsAsync()).ToDictionary(w => w.WalletID);
                var exchanges = (await exchangeRepo.GetAllAsync()).ToDictionary(e => e.ExchangeID);

                foreach (var position in openPositions)
                {
                    if (!wallets.TryGetValue(position.WalletID, out var wallet) ||
                        !exchanges.TryGetValue(wallet.ExchangeID, out var exchange))
                    {
                        _logger.LogError("Wallet or exchange not found for position {PositionID}", position.PositionID);
                        continue;
                    }
                    //if (!await positionRepo.HasOpenPositionAsync(position.WalletID, position.Symbol))
                    //{
                    //    position.Status = PositionStatus.Closed.ToString();
                    //    position.CloseTimestamp = DateTime.UtcNow;
                    //    await positionRepo.UpdateAsync(position);
                    //    continue; // این ولت برای این کوین پوزیشن باز دارد
                    //}

                    var exchangeService = exchangeFactory.CreateService(wallet, exchange);
                    bool actionSuccess = false;
                    decimal actionPrice = await exchangeService.GetLastPriceAsync(position.Symbol);


                    if (!position.ExitStrategyID.HasValue || !strategies.TryGetValue(position.ExitStrategyID.Value, out var exitStrategy))
                    {
                        _logger.LogWarning("Exit strategy for position {PositionID} is not defined or found.", position.PositionID);
                        continue;
                    }

                    var strategyHandler = strategyFactory.CreateExitStrategy(exitStrategy);
                    var signal = await strategyHandler.ExecuteAsync(position);

                    if (signal.Signal == SignalType.Hold) continue;

                    

                    switch (signal.Signal)
                    {
                        case SignalType.CloseByTP:
                        case SignalType.CloseBySL:
                            var closeResult = await exchangeService.ClosePositionAsync(position.Symbol, position.PositionSide, position.CurrentQuantity);
                            if (closeResult.IsSuccess)
                            {
                                position.ProfitUSD = (position.ProfitUSD ?? 0) + (actionPrice - position.EntryPrice) * position.CurrentQuantity * (position.PositionSide == SignalType.OpenLong.ToString() ? 1 : -1);
                                position.Status = PositionStatus.Closed.ToString();
                                position.CloseTimestamp = DateTime.UtcNow;
                                position.CurrentQuantity = 0;
                                await positionRepo.UpdateAsync(position);
                                actionSuccess = true;
                            }
                            break;

                        case SignalType.PartialClose:
                            if (signal.PartialPercent.HasValue && signal.PartialPercent > 0)
                            {
                                decimal quantityToClose = position.CurrentQuantity * (signal.PartialPercent.Value / 100);
                                var filterInfo = await exchangeService.GetSymbolFilterInfoAsync(position.Symbol);
                                if (filterInfo != null) quantityToClose = Math.Floor(quantityToClose / filterInfo.StepSize) * filterInfo.StepSize;

                                if (quantityToClose > 0)
                                {
                                    var sellResult = await exchangeService.ModifyPositionAsync(position.Symbol, "SELL", quantityToClose);
                                    if (sellResult.IsSuccess)
                                    {
                                        decimal realizedProfit = (actionPrice - position.EntryPrice) * sellResult.Quantity * (position.PositionSide == SignalType.OpenLong.ToString() ? 1 : -1);
                                        position.CurrentQuantity -= sellResult.Quantity;
                                        position.ProfitUSD = (position.ProfitUSD ?? 0) + realizedProfit;
                                        if (position.CurrentQuantity <= 0)
                                        {
                                            position.Status = PositionStatus.Closed.ToString();
                                            position.CloseTimestamp = DateTime.UtcNow;
                                        }
                                        await positionRepo.UpdateAsync(position);
                                        actionSuccess = true;
                                    }
                                }
                            }
                            break;

                        case SignalType.ChangeSL:
                            if (signal.NewStopLossPrice.HasValue)
                            {
                                var slResult = await positionRepo.UpdateStopLossAsync(position.PositionID,signal.NewStopLossPrice.Value);
                                actionSuccess = true;
                            }
                            break;
                    }

                    if (actionSuccess)
                    {
                        await telegramNotifier.SendNotificationCloseAsync(signal, wallet.WalletName, actionPrice, position);

                        var history = new PositionHistory
                        {
                            PositionID = position.PositionID,
                            ActionType = signal.Signal,
                            PercentPosition = signal.PartialPercent ?? 100,
                            Price = actionPrice,
                            ActionTimestamp = DateTime.UtcNow,
                            Description = signal.Reason,
                            Profit = position.ProfitUSD
                        };
                        await positionRepo.AddHistoryAsync(history);

                        _logger.LogInformation("Executed signal {Signal} for position {PositionID}. Reason: {Reason}",
                            signal.Signal, position.PositionID, signal.Reason);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
        }
    }
}
