// src/SmartTrader.Application/Models/SymbolFilterInfo.cs
namespace SmartTrader.Application.Models
{
    public class SymbolFilterInfo
    {
        public decimal StepSize { get; set; } // For LOT_SIZE filter
        public decimal MinQuantity { get; set; } // For LOT_SIZE filter
        public decimal TickSize { get; set; } // For PRICE_FILTER filter
    }
}