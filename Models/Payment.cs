using System;

namespace ThienThaiShop.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }

        public int OrderId { get; set; }

        public decimal Amount { get; set; }

        public string Method { get; set; }

        public string Status { get; set; }

        public string TransactionCode { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual Order Order { get; set; }
    }
}