// src/SmartTrader.Application/Models/Kline.cs
using System;

namespace SmartTrader.Application.Models
{
    public class Kline
    {
        public DateTime OpenTime { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal ClosePrice { get; set; }
        public decimal Volume { get; set; }
    }
}