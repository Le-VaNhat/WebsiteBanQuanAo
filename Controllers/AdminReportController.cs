using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class AdminReportController : Controller
    {
        private readonly ThienThaiDbContext db =
            new ThienThaiDbContext();

        private readonly string[] FixedSizeNames =
        {
            "S",
            "M",
            "L",
            "XL",
            "2XL"
        };

        private const int RestockThreshold = 5;
        private const int TargetStock = 10;

        // =====================================================
        // KIỂM TRA ADMIN
        // =====================================================

        private bool IsAdmin()
        {
            return Session["UserId"] != null
                && Session["Role"] != null
                && string.Equals(
                    Session["Role"].ToString(),
                    "Admin",
                    StringComparison.OrdinalIgnoreCase
                );
        }

        // =====================================================
        // BÁO CÁO DOANH THU + TỒN KHO
        // =====================================================

        public ActionResult Index(
            string search,
            string category,
            string filterType,
            DateTime? selectedDate,
            DateTime? selectedFromDate,
            DateTime? selectedToDate,
            int? selectedMonth,
            int? selectedYear,
            bool onlyRestock = false)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            search =
                string.IsNullOrWhiteSpace(search)
                    ? null
                    : search.Trim();

            category =
                string.IsNullOrWhiteSpace(category)
                    ? null
                    : category.Trim();

            filterType =
                string.IsNullOrWhiteSpace(filterType)
                    ? "all"
                    : filterType.Trim().ToLower();

            // =================================================
            // LẤY SẢN PHẨM
            // =================================================

            var productsQuery =
                db.Products
                    .Include(p => p.Category)
                    .Include(p => p.ProductSizes)
                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                productsQuery =
                    productsQuery.Where(p =>
                        p.Name.Contains(search)
                    );
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                productsQuery =
                    productsQuery.Where(p =>
                        p.Category != null &&
                        p.Category.Name == category
                    );
            }

            var products =
                productsQuery
                    .OrderBy(p => p.Name)
                    .ToList();

            // =================================================
            // LẤY 5 SIZE CỐ ĐỊNH
            // =================================================

            var sizes =
                db.Sizes
                    .Where(s =>
                        s.Name == "S" ||
                        s.Name == "M" ||
                        s.Name == "L" ||
                        s.Name == "XL" ||
                        s.Name == "2XL")
                    .ToList();

            // Tránh lỗi nếu DB có Size trùng tên.
            var sizeDictionary =
                sizes
                    .GroupBy(s =>
                        (s.Name ?? "")
                            .Trim()
                            .ToUpper()
                    )
                    .ToDictionary(
                        g => g.Key,
                        g => g.First()
                    );

            // =================================================
            // SỬA DỮ LIỆU SIZE CHO TẤT CẢ SẢN PHẨM
            // =================================================

            /*
             * Đây là phần quan trọng nhất.
             *
             * Trước đây:
             *
             * Không có ProductSize
             *      =>
             * "Chưa thiết lập"
             *
             * Bây giờ:
             *
             * - Tạo đủ S/M/L/XL/2XL.
             * - Lấy Product.Stock làm nguồn tồn kho ban đầu
             *   nếu sản phẩm chưa có dữ liệu Size.
             * - Nếu đã có một số Size thì giữ nguyên dữ liệu
             *   đã có và phân bổ phần còn lại cho Size thiếu.
             */

            bool stockDataChanged = false;

            foreach (var product in products)
            {
                bool changed =
                    EnsureAllProductSizes(product, sizeDictionary);

                if (changed)
                    stockDataChanged = true;
            }

            if (stockDataChanged)
            {
                db.SaveChanges();
            }

            // =================================================
            // LẤY LẠI PRODUCT SIZE SAU KHI ĐỒNG BỘ
            // =================================================

            var productSizes =
                db.ProductSizes
                    .Include(ps => ps.Size)
                    .Where(ps =>
                        ps.Size != null &&
                        (
                            ps.Size.Name == "S" ||
                            ps.Size.Name == "M" ||
                            ps.Size.Name == "L" ||
                            ps.Size.Name == "XL" ||
                            ps.Size.Name == "2XL"
                        ))
                    .ToList();

            // =================================================
            // TỒN KHO THEO PRODUCT + SIZE
            // =================================================

            var stockDictionary =
                productSizes
                    .GroupBy(ps => new
                    {
                        ps.ProductId,
                        ps.SizeId
                    })
                    .ToDictionary(
                        g => g.Key,
                        g => g.Sum(x => x.Stock)
                    );

            // =================================================
            // XÁC ĐỊNH KHOẢNG THỜI GIAN
            // =================================================

            DateTime? fromDate = null;
            DateTime? toDate = null;

            DateTime now = DateTime.Now;

            switch (filterType)
            {
                // -------------------------------------------------
                // HÔM NAY
                // -------------------------------------------------

                case "today":

                    fromDate =
                        now.Date;

                    toDate =
                        now.Date.AddDays(1);

                    break;

                // -------------------------------------------------
                // MỘT NGÀY
                // -------------------------------------------------

                case "day":

                    if (selectedDate.HasValue)
                    {
                        fromDate =
                            selectedDate.Value.Date;

                        toDate =
                            selectedDate.Value.Date.AddDays(1);
                    }

                    break;

                // -------------------------------------------------
                // TỪ NGÀY ĐẾN NGÀY
                // -------------------------------------------------

                case "range":

                    if (selectedFromDate.HasValue &&
                        selectedToDate.HasValue)
                    {
                        DateTime start =
                            selectedFromDate.Value.Date;

                        DateTime end =
                            selectedToDate.Value.Date;

                        if (start > end)
                        {
                            DateTime temp = start;

                            start = end;
                            end = temp;
                        }

                        fromDate =
                            start;

                        // Bao gồm toàn bộ ngày đến.
                        toDate =
                            end.AddDays(1);
                    }
                    else if (selectedFromDate.HasValue)
                    {
                        fromDate =
                            selectedFromDate.Value.Date;

                        toDate =
                            now.Date.AddDays(1);
                    }
                    else if (selectedToDate.HasValue)
                    {
                        // Không dùng DateTime.MinValue vì
                        // SQL Server datetime không hỗ trợ
                        // ngày nhỏ hơn 1753-01-01.

                        fromDate =
                            new DateTime(
                                1753,
                                1,
                                1
                            );

                        toDate =
                            selectedToDate.Value.Date.AddDays(1);
                    }

                    break;

                // -------------------------------------------------
                // THÁNG
                // -------------------------------------------------

                case "month":

                    if (selectedYear.HasValue &&
                        selectedMonth.HasValue &&
                        selectedMonth.Value >= 1 &&
                        selectedMonth.Value <= 12)
                    {
                        fromDate =
                            new DateTime(
                                selectedYear.Value,
                                selectedMonth.Value,
                                1
                            );

                        toDate =
                            fromDate.Value.AddMonths(1);
                    }

                    break;

                // -------------------------------------------------
                // NĂM
                // -------------------------------------------------

                case "year":

                    if (selectedYear.HasValue)
                    {
                        fromDate =
                            new DateTime(
                                selectedYear.Value,
                                1,
                                1
                            );

                        toDate =
                            fromDate.Value.AddYears(1);
                    }

                    break;

                // -------------------------------------------------
                // TẤT CẢ
                // -------------------------------------------------

                case "all":

                default:

                    fromDate = null;
                    toDate = null;

                    break;
            }

            // =================================================
            // ĐƠN HÀNG TÍNH DOANH THU
            // =================================================

            var orderDetailsQuery =
                db.OrderDetails
                    .Include(od => od.Order)
                    .AsQueryable();

            orderDetailsQuery =
                orderDetailsQuery.Where(od =>
                    od.Order != null &&
                    (
                        od.Order.Status == "Completed" ||
                        od.Order.Status == "Delivered"
                    )
                );

            // =================================================
            // LỌC THEO NGÀY
            // =================================================

            if (fromDate.HasValue &&
                toDate.HasValue)
            {
                DateTime start =
                    fromDate.Value;

                DateTime end =
                    toDate.Value;

                orderDetailsQuery =
                    orderDetailsQuery.Where(od =>
                        od.Order.CreatedAt >= start &&
                        od.Order.CreatedAt < end
                    );
            }

            // =================================================
            // GROUP DOANH THU PRODUCT + SIZE
            // =================================================

            var salesData =
                orderDetailsQuery
                    .GroupBy(od => new
                    {
                        od.ProductId,
                        od.SizeId
                    })
                    .Select(g => new
                    {
                        ProductId =
                            g.Key.ProductId,

                        SizeId =
                            g.Key.SizeId,

                        QuantitySold =
                            g.Sum(x => x.Quantity),

                        Revenue =
                            g.Sum(
                                x =>
                                    x.UnitPrice *
                                    x.Quantity
                            )
                    })
                    .ToList();

            var salesDictionary =
                salesData.ToDictionary(
                    x => new
                    {
                        x.ProductId,
                        x.SizeId
                    },
                    x => new
                    {
                        x.QuantitySold,
                        x.Revenue
                    }
                );

            // =================================================
            // TẠO BẢNG BÁO CÁO
            // =================================================

            var rows =
                new List<AdminSalesReportRowViewModel>();

            foreach (var product in products)
            {
                foreach (var sizeName in FixedSizeNames)
                {
                    string normalizedSize =
                        sizeName
                            .Trim()
                            .ToUpper();

                    if (!sizeDictionary.ContainsKey(
                        normalizedSize))
                    {
                        continue;
                    }

                    var size =
                        sizeDictionary[
                            normalizedSize
                        ];

                    // =========================================
                    // TỒN KHO
                    // =========================================

                    var stockKey = new
                    {
                        ProductId =
                            product.ProductId,

                        SizeId =
                            size.SizeId
                    };

                    int currentStock = 0;

                    if (stockDictionary.ContainsKey(
                        stockKey))
                    {
                        currentStock =
                            stockDictionary[
                                stockKey
                            ];
                    }

                    // =========================================
                    // ĐÃ BÁN + DOANH THU
                    // =========================================

                    var saleKey = new
                    {
                        ProductId =
                            product.ProductId,

                        SizeId =
                            size.SizeId
                    };

                    int quantitySold = 0;

                    decimal revenue = 0;

                    if (salesDictionary.ContainsKey(
                        saleKey))
                    {
                        quantitySold =
                            salesDictionary[
                                saleKey
                            ].QuantitySold;

                        revenue =
                            salesDictionary[
                                saleKey
                            ].Revenue;
                    }

                    // =========================================
                    // THÊM DÒNG
                    // =========================================

                    rows.Add(
                        new AdminSalesReportRowViewModel
                        {
                            ProductId =
                                product.ProductId,

                            ProductName =
                                product.Name,

                            CategoryName =
                                product.Category != null
                                    ? product.Category.Name
                                    : "Chưa phân loại",

                            SizeName =
                                size.Name,

                            UnitPrice =
                                product.Price,

                            QuantitySold =
                                quantitySold,

                            Revenue =
                                revenue,

                            CurrentStock =
                                currentStock,

                            // Từ giờ controller luôn tạo
                            // đủ ProductSize nên không còn
                            // "Chưa thiết lập".
                            IsSizeConfigured =
                                true
                        }
                    );
                }
            }

            // =================================================
            // CHỈ HIỆN SIZE CẦN NHẬP
            // =================================================

            if (onlyRestock)
            {
                rows =
                    rows
                        .Where(r =>
                            r.ShouldRestock)
                        .ToList();
            }

            // =================================================
            // SẮP XẾP
            // =================================================

            rows =
                rows
                    .OrderByDescending(
                        r => r.ShouldRestock
                    )
                    .ThenByDescending(
                        r => r.QuantitySold
                    )
                    .ThenBy(
                        r => r.ProductName
                    )
                    .ThenBy(
                        r => GetSizeOrder(
                            r.SizeName
                        )
                    )
                    .ToList();

            // =================================================
            // TỔNG QUAN
            // =================================================

            var model =
                new AdminSalesReportViewModel();

            model.Rows =
                rows;

            model.TotalRevenue =
                rows.Sum(r => r.Revenue);

            model.TotalSold =
                rows.Sum(r => r.QuantitySold);

            model.TotalStock =
                rows.Sum(r => r.CurrentStock);

            model.RestockSizeCount =
                rows.Count(
                    r => r.ShouldRestock
                );

            model.RestockQuantity =
                rows.Sum(
                    r => r.SuggestedRestock
                );

            // =================================================
            // DANH MỤC
            // =================================================

            var categories =
                db.Categories
                    .Where(c =>
                        c.Name != null &&
                        c.Name != "")
                    .GroupBy(c => c.Name)
                    .Select(g =>
                        g.FirstOrDefault())
                    .OrderBy(c => c.Name)
                    .ToList();

            // =================================================
            // VIEWBAG
            // =================================================

            ViewBag.Categories =
                categories;

            ViewBag.Search =
                search;

            ViewBag.SelectedCategory =
                category;

            ViewBag.FilterType =
                filterType;

            ViewBag.SelectedDate =
                selectedDate.HasValue
                    ? selectedDate.Value.ToString(
                        "yyyy-MM-dd")
                    : "";

            ViewBag.SelectedFromDate =
                selectedFromDate.HasValue
                    ? selectedFromDate.Value.ToString(
                        "yyyy-MM-dd")
                    : "";

            ViewBag.SelectedToDate =
                selectedToDate.HasValue
                    ? selectedToDate.Value.ToString(
                        "yyyy-MM-dd")
                    : "";

            ViewBag.SelectedMonth =
                selectedMonth;

            ViewBag.SelectedYear =
                selectedYear;

            ViewBag.OnlyRestock =
                onlyRestock;

            ViewBag.FromDate =
                fromDate;

            ViewBag.ToDate =
                toDate;

            return View(model);
        }

        // =====================================================
        // TẠO ĐỦ PRODUCT SIZE VÀ ĐỒNG BỘ TỒN KHO
        // =====================================================

        private bool EnsureAllProductSizes(
            Product product,
            Dictionary<string, Size> sizeDictionary)
        {
            bool changed = false;

            if (product == null)
                return false;

            if (product.ProductSizes == null)
            {
                product.ProductSizes =
                    new HashSet<ProductSize>();
            }

            // -------------------------------------------------
            // LẤY CÁC PRODUCT SIZE HIỆN CÓ
            // -------------------------------------------------

            var existingBySize =
                product.ProductSizes
                    .Where(ps =>
                        ps.Size != null &&
                        FixedSizeNames.Contains(
                            ps.Size.Name.Trim(),
                            StringComparer.OrdinalIgnoreCase
                        ))
                    .GroupBy(ps =>
                        ps.Size.Name
                            .Trim()
                            .ToUpper())
                    .ToDictionary(
                        g => g.Key,
                        g => g.First()
                    );

            // -------------------------------------------------
            // TỔNG TỒN KHO SIZE ĐANG CÓ
            // -------------------------------------------------

            int existingStock =
                existingBySize.Values
                    .Sum(ps => Math.Max(0, ps.Stock));

            // -------------------------------------------------
            // TỒN KHO GỐC CỦA PRODUCT
            // -------------------------------------------------

            int productStock =
                Math.Max(
                    0,
                    product.Stock
                );

            // -------------------------------------------------
            // XÁC ĐỊNH PHẦN TỒN KHO CÒN LẠI
            // -------------------------------------------------

            int remainingStock;

            if (existingBySize.Count == 0)
            {
                // Chưa có Size nào:
                // lấy toàn bộ Product.Stock để chia.
                remainingStock =
                    productStock;
            }
            else
            {
                /*
                 * Nếu ProductSize đã có dữ liệu,
                 * ưu tiên dữ liệu ProductSize.
                 *
                 * Nếu Product.Stock lớn hơn tổng Size hiện có,
                 * phần chênh lệch sẽ chia cho Size còn thiếu.
                 */

                if (productStock > existingStock)
                {
                    remainingStock =
                        productStock -
                        existingStock;
                }
                else
                {
                    remainingStock = 0;
                }
            }

            // -------------------------------------------------
            // TÌM SIZE CÒN THIẾU
            // -------------------------------------------------

            var missingSizes =
                FixedSizeNames
                    .Where(name =>
                        !existingBySize.ContainsKey(
                            name.ToUpper()))
                    .ToList();

            // -------------------------------------------------
            // CHIA PHẦN TỒN KHO CHO SIZE THIẾU
            // -------------------------------------------------

            int missingCount =
                missingSizes.Count;

            int baseStock = 0;
            int remainder = 0;

            if (missingCount > 0 &&
                remainingStock > 0)
            {
                baseStock =
                    remainingStock /
                    missingCount;

                remainder =
                    remainingStock %
                    missingCount;
            }

            // -------------------------------------------------
            // TẠO PRODUCT SIZE CÒN THIẾU
            // -------------------------------------------------

            int missingIndex = 0;

            foreach (string sizeName in missingSizes)
            {
                string normalizedSize =
                    sizeName
                        .Trim()
                        .ToUpper();

                if (!sizeDictionary.ContainsKey(
                    normalizedSize))
                {
                    continue;
                }

                Size size =
                    sizeDictionary[
                        normalizedSize
                    ];

                int newStock =
                    baseStock;

                // Phần dư cộng lần lượt từ Size đầu.
                if (missingIndex < remainder)
                {
                    newStock++;
                }

                var newProductSize =
                    new ProductSize
                    {
                        ProductId =
                            product.ProductId,

                        SizeId =
                            size.SizeId,

                        Stock =
                            newStock
                    };

                db.ProductSizes.Add(
                    newProductSize
                );

                product.ProductSizes.Add(
                    newProductSize
                );

                existingBySize[
                    normalizedSize
                ] =
                    newProductSize;

                missingIndex++;

                changed = true;
            }

            // -------------------------------------------------
            // ĐỒNG BỘ PRODUCT.STOCK
            // -------------------------------------------------

            int totalSizeStock =
                existingBySize.Values
                    .Sum(ps =>
                        Math.Max(
                            0,
                            ps.Stock
                        ));

            /*
             * Product.Stock phải bằng tổng tồn kho
             * của tất cả Size.
             *
             * Như vậy:
             *
             * Trang sản phẩm
             *        ↓
             * Product.Stock
             *
             * Báo cáo tồn kho
             *        ↓
             * ProductSize.Stock
             *
             * luôn khớp nhau.
             */

            if (product.Stock != totalSizeStock)
            {
                product.Stock =
                    totalSizeStock;

                changed = true;
            }

            return changed;
        }

        // =====================================================
        // THỨ TỰ SIZE
        // =====================================================

        private int GetSizeOrder(
            string sizeName)
        {
            switch (
                (sizeName ?? "")
                    .Trim()
                    .ToUpper())
            {
                case "S":
                    return 1;

                case "M":
                    return 2;

                case "L":
                    return 3;

                case "XL":
                    return 4;

                case "2XL":
                    return 5;

                default:
                    return 99;
            }
        }

        // =====================================================
        // DISPOSE
        // =====================================================

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}