using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// src/SmartTrader.Domain/Entities/Coin.cs
namespace SmartTrader.Domain.Entities
{
    public class Coin
    {
        public int CoinID { get; set; }
        public string CoinName { get; set; }
        public string Symbol { get; set; }
        public string? CoinType { get; set; }
        public string BaseCurrency { get; set; }
        public string? ExchangeInfoJson { get; set; }
    }
}
