using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ThienThaiShop.Models
{
    public class Product
    {
        public int ProductId { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [Required]
        [Column(TypeName = "decimal")]
        public decimal Price { get; set; }

        public string Description { get; set; }

        public string ImageUrl { get; set; }

        public int Stock { get; set; }

        public bool IsActive { get; set; }

        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }
    }
}