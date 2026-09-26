using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class ProductController : Controller
    {
        private readonly ThienThaiDbContext db = new ThienThaiDbContext();

        private readonly string[] FixedCategoryNames =
        {
            "Áo nam",
            "Quần nam",
            "Áo nữ",
            "Quần nữ"
        };

        private readonly string[] FixedSizeNames =
        {
            "S",
            "M",
            "L",
            "XL",
            "2XL"
        };

        public ActionResult Index(string search, string category, int? categoryId)
        {
            var products = db.Products
                .Include(p => p.Category)
                .AsQueryable();

            if (categoryId.HasValue && string.IsNullOrWhiteSpace(category))
            {
                var oldCategory = db.Categories
                    .FirstOrDefault(c => c.CategoryId == categoryId.Value);

                if (oldCategory != null &&
                    FixedCategoryNames.Contains(oldCategory.Name))
                {
                    category = oldCategory.Name;
                }
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                products = products.Where(p =>
                    p.Name.Contains(search) ||
                    (p.Description != null &&
                     p.Description.Contains(search))
                );
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                category = category.Trim();

                if (FixedCategoryNames.Contains(category))
                {
                    products = products.Where(p =>
                        p.Category != null &&
                        p.Category.Name == category
                    );
                }
                else
                {
                    category = null;
                }
            }
            else
            {
                products = products.Where(p =>
                    p.Category != null &&
                    FixedCategoryNames.Contains(p.Category.Name)
                );
            }

            var productList = products
                .OrderByDescending(p => p.ProductId)
                .ToList();

            var categories = new List<Category>();

            foreach (string categoryName in FixedCategoryNames)
            {
                var fixedCategory = db.Categories
                    .FirstOrDefault(c => c.Name == categoryName);

                if (fixedCategory != null)
                {
                    categories.Add(fixedCategory);
                }
            }

            ViewBag.Search = search;
            ViewBag.SelectedCategory = category;
            ViewBag.Categories = categories;

            return View(productList);
        }

        public ActionResult BestSeller()
        {
            var products = db.Products
                .Include(p => p.Category)
                .Where(p =>
                    p.Category != null &&
                    FixedCategoryNames.Contains(p.Category.Name))
                .OrderByDescending(p => p.Stock)
                .ThenByDescending(p => p.ProductId)
                .Take(20)
                .ToList();

            var categories = new List<Category>();

            foreach (string categoryName in FixedCategoryNames)
            {
                var fixedCategory = db.Categories
                    .FirstOrDefault(c => c.Name == categoryName);

                if (fixedCategory != null)
                {
                    categories.Add(fixedCategory);
                }
            }

            ViewBag.Categories = categories;

            return View(products);
        }

        public ActionResult Details(int? id)
        {
            if (!id.HasValue)
            {
                return RedirectToAction("Index", "Product");
            }

            EnsureDefaultSizes();

            var product = db.Products
                .Include(p => p.Category)
                .Include(p => p.ProductSizes.Select(ps => ps.Size))
                .FirstOrDefault(p => p.ProductId == id.Value);

            if (product == null)
            {
                return HttpNotFound("Không tìm thấy sản phẩm.");
            }

            if (product.Category == null ||
                !FixedCategoryNames.Contains(product.Category.Name))
            {
                return HttpNotFound(
                    "Sản phẩm không thuộc danh mục của cửa hàng."
                );
            }

            // Đồng bộ tồn kho theo size cho sản phẩm cũ
            EnsureProductSizeStock(product);

            // Load lại ProductSizes sau khi có thể vừa tạo dữ liệu
            db.Entry(product)
                .Collection(p => p.ProductSizes)
                .Query()
                .Include(ps => ps.Size)
                .Load();

            var sizes = db.Sizes
                .Where(s =>
                    s.Name == "S" ||
                    s.Name == "M" ||
                    s.Name == "L" ||
                    s.Name == "XL" ||
                    s.Name == "2XL")
                .ToList()
                .OrderBy(s => GetSizeOrder(s.Name))
                .ToList();

            ViewBag.Sizes = sizes;

            var sizeStocks = new Dictionary<int, int>();

            foreach (var size in sizes)
            {
                var productSize = product.ProductSizes
                    .FirstOrDefault(ps => ps.SizeId == size.SizeId);

                sizeStocks[size.SizeId] =
                    productSize != null
                        ? productSize.Stock
                        : 0;
            }

            ViewBag.SizeStocks = sizeStocks;

            // Tổng tồn thực tế theo size
            int totalSizeStock = sizeStocks.Values.Sum();

            ViewBag.TotalSizeStock = totalSizeStock;

            // Đồng bộ Product.Stock theo tổng tồn size
            if (product.Stock != totalSizeStock)
            {
                product.Stock = totalSizeStock;
                db.SaveChanges();
            }

            return View(product);
        }

        private void EnsureDefaultSizes()
        {
            bool changed = false;

            foreach (string sizeName in FixedSizeNames)
            {
                bool exists = db.Sizes.Any(s => s.Name == sizeName);

                if (!exists)
                {
                    db.Sizes.Add(new Size
                    {
                        Name = sizeName
                    });

                    changed = true;
                }
            }

            if (changed)
            {
                db.SaveChanges();
            }
        }

        private void EnsureProductSizeStock(Product product)
        {
            var sizes = db.Sizes
                .Where(s =>
                    s.Name == "S" ||
                    s.Name == "M" ||
                    s.Name == "L" ||
                    s.Name == "XL" ||
                    s.Name == "2XL")
                .ToList()
                .OrderBy(s => GetSizeOrder(s.Name))
                .ToList();

            if (sizes.Count == 0)
            {
                return;
            }

            var productSizes = db.ProductSizes
                .Where(ps => ps.ProductId == product.ProductId)
                .ToList();

            /*
             * Trường hợp 1:
             * Sản phẩm chưa có ProductSize.
             *
             * Lấy Product.Stock chia cho 5 size.
             */
            if (productSizes.Count == 0)
            {
                CreateProductSizeFromTotalStock(
                    product.ProductId,
                    product.Stock,
                    sizes
                );

                return;
            }

            /*
             * Trường hợp 2:
             * Có ProductSize nhưng tất cả đều bằng 0,
             * trong khi Product.Stock vẫn còn hàng.
             *
             * Đây chính là trường hợp sản phẩm của bạn
             * đang gặp phải.
             */
            int totalProductSizeStock = productSizes.Sum(ps => ps.Stock);

            if (totalProductSizeStock <= 0 && product.Stock > 0)
            {
                CreateProductSizeFromTotalStock(
                    product.ProductId,
                    product.Stock,
                    sizes
                );

                return;
            }

            /*
             * Trường hợp 3:
             * ProductSize đã có tồn thực tế > 0.
             * Không tự ý ghi đè dữ liệu tồn theo size.
             */
        }

        private void CreateProductSizeFromTotalStock(
            int productId,
            int totalStock,
            List<Size> sizes)
        {
            if (totalStock < 0)
            {
                totalStock = 0;
            }

            /*
             * Xóa các ProductSize cũ đang sai dữ liệu
             * rồi tạo lại theo Product.Stock.
             */
            var oldProductSizes = db.ProductSizes
                .Where(ps => ps.ProductId == productId)
                .ToList();

            if (oldProductSizes.Count > 0)
            {
                db.ProductSizes.RemoveRange(oldProductSizes);
                db.SaveChanges();
            }

            int baseStock = totalStock / sizes.Count;
            int remainder = totalStock % sizes.Count;

            for (int i = 0; i < sizes.Count; i++)
            {
                int stockForSize = baseStock;

                if (i < remainder)
                {
                    stockForSize++;
                }

                db.ProductSizes.Add(new ProductSize
                {
                    ProductId = productId,
                    SizeId = sizes[i].SizeId,
                    Stock = stockForSize
                });
            }

            db.SaveChanges();
        }

        private int GetSizeOrder(string sizeName)
        {
            switch (sizeName)
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}