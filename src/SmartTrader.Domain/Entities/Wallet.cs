using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// src/SmartTrader.Domain/Entities/Wallet.cs
namespace SmartTrader.Domain.Entities
{
    public class Wallet
    {
        public int WalletID { get; set; }
        public string WalletName { get; set; }
        public int ExchangeID { get; set; }
        public string ApiKey { get; set; }
        public string SecretKey { get; set; }
        public int MaxLeverage { get; set; }
        public decimal MaxBalancePercentToTrade { get; set; }
        public bool IsActive { get; set; }
    }
}
