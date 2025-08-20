// src/SmartTrader.Domain/Entities/Wallet.cs
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
        public int? ForceExitStrategyID { get; set; }
        public bool IsActive { get; set; }
    }
}