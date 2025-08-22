// src/SmartTrader.Infrastructure/Services/TelegramNotifier.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Models;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SmartTrader.Infrastructure.Services
{
    public class TelegramNotifier : ITelegramNotifier
    {
        private readonly ILogger<TelegramNotifier> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _botToken;
        private readonly string _channelId;

        public TelegramNotifier(ILogger<TelegramNotifier> logger, IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _botToken = configuration["TelegramSettings:BotToken"];
            _channelId = configuration["TelegramSettings:ChannelId"];
        }

        public async Task SendNotificationAsync(StrategySignal signal, string coinName, string strategyName)
        {
            if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_channelId))
            {
                _logger.LogWarning("Telegram BotToken or ChannelId is not configured. Skipping notification.");
                return;
            }

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"🚨 **New Trading Signal** 🚨");
            messageBuilder.AppendLine($"**Strategy:** `{strategyName}`");
            messageBuilder.AppendLine($"**Coin:** `{coinName}`");
            messageBuilder.AppendLine($"**Signal:** `{signal.Signal}`");
            messageBuilder.AppendLine($"**Reason:** {signal.Reason}");
            messageBuilder.AppendLine($"");
            messageBuilder.AppendLine($"**Parameters:**");
            messageBuilder.AppendLine($"- Leverage: `{signal.Leverage ?? 0}x`");
            messageBuilder.AppendLine($"- Balance %: `{signal.PercentBalance ?? 0}%`");
            messageBuilder.AppendLine($"- Stop Loss: `{signal.StopLoss ?? 0}%`");
            messageBuilder.AppendLine($"- Take Profit: `{signal.TakeProfit ?? 0}%`");

            var message = messageBuilder.ToString();
            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage?chat_id={_channelId}&text={Uri.EscapeDataString(message)}&parse_mode=Markdown";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully sent notification to Telegram channel {ChannelId}", _channelId);
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to send Telegram notification. Status: {StatusCode}, Response: {Response}", response.StatusCode, responseContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while sending Telegram notification.");
            }
        }
    }
}