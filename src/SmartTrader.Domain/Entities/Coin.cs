// src/SmartTrader.Domain/Entities/Coin.cs
using System.Collections.Generic;
using System.Text.Json;

namespace SmartTrader.Domain.Entities
{
    public class Coin
    {
        public int CoinID { get; set; }
        public string CoinName { get; set; }
        public string BaseCurrency { get; set; }
        public string ExchangeInfoJson { get; set; }

        public List<CoinExchangeInfo> GetExchangeInfo()
        {
            if (string.IsNullOrEmpty(ExchangeInfoJson))
                return new List<CoinExchangeInfo>();
            
            return JsonSerializer.Deserialize<List<CoinExchangeInfo>>(ExchangeInfoJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
    }
}
