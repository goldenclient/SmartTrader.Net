using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// src/SmartTrader.Domain/Entities/PositionHistory.cs
using System;

namespace SmartTrader.Domain.Entities
{
    public class PositionHistory
    {
        public long PositionHistoryID { get; set; }
        public int PositionID { get; set; }
        public string ActionType { get; set; } // "OPEN", "CLOSE", etc.
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime ActionTimestamp { get; set; }
        public string? Description { get; set; }
    }
}