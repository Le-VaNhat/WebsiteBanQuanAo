using System.ComponentModel.DataAnnotations;

namespace ThienThaiShop.Models
{
    public class Size
    {
        public int SizeId { get; set; }

        [Required]
        [StringLength(20)]
        public string Name { get; set; }
    }
}