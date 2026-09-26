using System.Collections.Generic;

namespace ThienThaiShop.Models
{
    public class AdminSalesReportRowViewModel
    {
        public int ProductId { get; set; }

        public string ProductName { get; set; }

        public string CategoryName { get; set; }

        public string SizeName { get; set; }

        public decimal UnitPrice { get; set; }

        public int QuantitySold { get; set; }

        public decimal Revenue { get; set; }

        public int CurrentStock { get; set; }

        public bool IsSizeConfigured { get; set; }

        public bool ShouldRestock
        {
            get
            {
                return IsSizeConfigured &&
                       CurrentStock <= 5;
            }
        }

        public int SuggestedRestock
        {
            get
            {
                if (!ShouldRestock)
                    return 0;

                return 10 - CurrentStock;
            }
        }

        public string StockStatus
        {
            get
            {
                if (!IsSizeConfigured)
                    return "Chưa thiết lập";

                if (CurrentStock <= 0)
                    return "Hết hàng";

                if (CurrentStock <= 5)
                    return "Nên nhập";

                return "Đủ hàng";
            }
        }
    }


    public class AdminSalesReportViewModel
    {
        public List<AdminSalesReportRowViewModel> Rows
        {
            get;
            set;
        }

        public decimal TotalRevenue
        {
            get;
            set;
        }

        public int TotalSold
        {
            get;
            set;
        }

        public int TotalStock
        {
            get;
            set;
        }

        public int RestockSizeCount
        {
            get;
            set;
        }

        public int RestockQuantity
        {
            get;
            set;
        }

        public AdminSalesReportViewModel()
        {
            Rows =
                new List<
                    AdminSalesReportRowViewModel
                >();
        }
    }
}