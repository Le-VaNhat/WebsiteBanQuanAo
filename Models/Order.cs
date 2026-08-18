using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThienThaiShop.Models
{
    public class Order
    {
        public int OrderId { get; set; }

        public int UserId { get; set; }

        [Required]
        [StringLength(255)]
        public string ShippingAddress { get; set; }

        [Required]
        [StringLength(20)]
        public string Phone { get; set; }

        public decimal TotalAmount { get; set; }

        [StringLength(50)]
        public string PaymentMethod { get; set; }

        [StringLength(50)]
        public string Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public virtual ApplicationUser User { get; set; }

        public virtual ICollection<OrderDetail> OrderDetails { get; set; }

        public Order()
        {
            OrderDetails = new HashSet<OrderDetail>();
        }
    }
}