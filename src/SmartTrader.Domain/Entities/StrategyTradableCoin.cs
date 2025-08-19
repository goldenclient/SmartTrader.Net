using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// src/SmartTrader.Domain/Entities/StrategyTradableCoin.cs
namespace SmartTrader.Domain.Entities
{
    public class StrategyTradableCoin
    {
        public int StrategyTradableCoinID { get; set; }
        public int StrategyID { get; set; }
        public int CoinID { get; set; }
        public int PriorityWeight { get; set; }
        public bool IsActive { get; set; }
    }
}

