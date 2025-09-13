// src/SmartTrader.Infrastructure/Services/TelegramNotifier.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartTrader.Application.Interfaces.Services;
using SmartTrader.Application.Models;
using SmartTrader.Domain.Entities;
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
        private readonly string _channelHistoryId;

        public TelegramNotifier(ILogger<TelegramNotifier> logger, IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _botToken = configuration["TelegramSettings:BotToken"];
            _channelId = configuration["TelegramSettings:ChannelId"];
            _channelHistoryId = configuration["TelegramSettings:ChannelHistoryId"];
        }

        public async Task SendNotificationAsync(StrategySignal signal, string coinName, string strategyName,string walletName,decimal price)
        {
            if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_channelId))
            {
                _logger.LogWarning("Telegram BotToken or ChannelId is not configured. Skipping notification.");
                return;
            }

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"🚨 **" + signal.Signal + " Signal [ " + coinName + " ]** 🚨");
            messageBuilder.AppendLine($"**WalletName:** `{walletName}`");
            messageBuilder.AppendLine($"**Strategy:** `{strategyName}`");
            messageBuilder.AppendLine($"**Price:** `{price}`");
            messageBuilder.AppendLine($"**Reason:** {signal.Reason}");
            messageBuilder.AppendLine($"");
            messageBuilder.AppendLine($"**Parameters:**");
            messageBuilder.AppendLine($"- Leverage: `{signal.Leverage ?? 0}x`");
            messageBuilder.AppendLine($"- Balance %: `{signal.PercentBalance ?? 0}%`");

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

        public async Task SendNotificationCloseAsync(StrategySignal signal, string walletName,decimal actionPrice,Position position)
        {
            if (string.IsNullOrEmpty(_botToken) || string.IsNullOrEmpty(_channelId))
            {
                _logger.LogWarning("Telegram BotToken or ChannelId is not configured. Skipping notification.");
                return;
            }

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"🚨 **"+signal.Signal+" [ "+position.Symbol+" ]** 🚨");
            messageBuilder.AppendLine($"**WalletName:** `{walletName}`");
            messageBuilder.AppendLine($"**Price:** `{actionPrice}`");
            messageBuilder.AppendLine($"**PosUSDT %:** `{position.EntryValueUSD}`");
            messageBuilder.AppendLine($"**Profit:** `{position.ProfitUSD}`");
            messageBuilder.AppendLine($"**Reason:** `{signal.Reason}`");

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

        public async Task SendNotificationHistoryAsync(string reason)
        {
            var url = $"https://api.telegram.org/bot{_botToken}/sendMessage?chat_id={_channelHistoryId}&text={Uri.EscapeDataString(reason)}&parse_mode=Markdown";
            try { var response = await _httpClient.GetAsync(url); }
            catch{}
        }
    }
}