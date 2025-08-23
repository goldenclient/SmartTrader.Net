// src/SmartTrader.Domain/Enums/SignalType.cs
namespace SmartTrader.Domain.Enums
{
    public enum SignalType
    {
        Hold,
        OpenLong,
        OpenShort,
        SellProfit,     // فروش بخشی در سود
        BuyRollback,    // خرید مجدد در بازگشت قیمت
        CloseByTP,      // بستن پوزیشن با حد سود
        CloseBySL,      // بستن پوزیشن با حد ضرر
        ChangeSL,       // تغییر حد ضرر
        ChangeTP        // تغییر حد سود
    }
}