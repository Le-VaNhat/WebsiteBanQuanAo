using System.Collections.Generic;

namespace ThienThaiShop.Models
{
    public class ChatProductViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; }
        public string CategoryName { get; set; }
        public decimal Price { get; set; }
        public string PriceText { get; set; }
        public string ImageUrl { get; set; }
        public int TotalStock { get; set; }
        public string DetailUrl { get; set; }
    }

    public class ChatResponseViewModel
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<ChatProductViewModel> Products { get; set; }

        public ChatResponseViewModel()
        {
            Products = new List<ChatProductViewModel>();
        }
    }
}
