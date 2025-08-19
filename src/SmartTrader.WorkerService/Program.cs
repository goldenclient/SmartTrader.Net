// src/SmartTrader.WorkerService/Program.cs
using SmartTrader.Application.Interfaces.Persistence;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Interfaces.Strategies;
using SmartTrader.Infrastructure.Persistence.Repositories;
using SmartTrader.Infrastructure.Services;
using SmartTrader.Infrastructure.Strategies;
using SmartTrader.WorkerService.Workers;

Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        // ثبت Repositories
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IStrategyRepository, StrategyRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IExchangeRepository, ExchangeRepository>(); // ریپازیتوری جدید

        // ثبت Factory ها
        services.AddScoped<IExchangeServiceFactory, ExchangeServiceFactory>();
        services.AddScoped<IStrategyFactory, StrategyFactory>();

        // ثبت Worker ها
        services.AddHostedService<OpenPositionWorker>();
        services.AddHostedService<ClosePositionWorker>();

        // ثبت کلاس‌های استراتژی به صورت Transient
        // services.AddTransient<TakeProfitStopLossExitStrategy>();
        // services.AddTransient<RsiMacdEntryStrategy>();

    })
    .Build()
    .Run();