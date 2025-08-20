// src/SmartTrader.Domain/Enums/ActionType.cs
namespace SmartTrader.Domain.Enums
{
    public enum ActionType
    {
        Buy,
        RaiseUp, // خرید مجدد یا افزایش حجم
        Sell,
        Rollback // فروش بخشی از حجم
    }
}
