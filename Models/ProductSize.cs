using System.ComponentModel.DataAnnotations.Schema;

namespace ThienThaiShop.Models
{
    public class ProductSize
    {
        public int ProductSizeId { get; set; }

        public int ProductId { get; set; }

        public int SizeId { get; set; }

        public int Stock { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        [ForeignKey("SizeId")]
        public virtual Size Size { get; set; }
    }
}