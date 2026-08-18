using System.Collections.Generic;

namespace ThienThaiShop.Models
{
    public class Cart
    {
        public int CartId { get; set; }

        public int UserId { get; set; }

        public virtual ApplicationUser User { get; set; }

        public virtual ICollection<CartItem> CartItems { get; set; }

        public Cart()
        {
            CartItems = new HashSet<CartItem>();
        }
    }
}