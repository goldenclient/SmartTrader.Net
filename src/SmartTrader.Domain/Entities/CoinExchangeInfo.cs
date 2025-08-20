// src/SmartTrader.Domain/Entities/CoinExchangeInfo.cs
namespace SmartTrader.Domain.Entities
{
    public class CoinExchangeInfo
    {
        public string Exchange { get; set; }
        public string Symbol { get; set; }
        public int Lot { get; set; }
    }
}