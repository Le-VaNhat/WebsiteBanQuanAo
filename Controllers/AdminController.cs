using System;
using System.Linq;
using System.Web.Mvc;
using System.Data.Entity;
using System.Collections.Generic;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class AdminController : Controller
    {
        private readonly ThienThaiDbContext db =
            new ThienThaiDbContext();

        // =====================================================
        // DANH MỤC CỐ ĐỊNH
        // =====================================================

        private readonly string[] FixedCategoryNames =
        {
            "Áo nam",
            "Quần nam",
            "Áo nữ",
            "Quần nữ"
        };

        // =====================================================
        // SIZE CỐ ĐỊNH
        // =====================================================

        private readonly string[] FixedSizeNames =
        {
            "S",
            "M",
            "L",
            "XL",
            "2XL"
        };

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
        // LẤY 4 DANH MỤC CỐ ĐỊNH
        // =====================================================

        private IQueryable<Category> GetFixedCategoriesQuery()
        {
            return db.Categories
                .Where(c =>
                    c.Name != null &&
                    FixedCategoryNames.Contains(c.Name));
        }

        private List<Category> GetFixedCategories()
        {
            var categories =
                db.Categories
                    .Where(c =>
                        c.Name != null &&
                        FixedCategoryNames.Contains(c.Name))
                    .ToList();

            return FixedCategoryNames
                .Select(name =>
                    categories.FirstOrDefault(
                        c => c.Name == name))
                .Where(c => c != null)
                .ToList();
        }

        // =====================================================
        // ĐẢM BẢO 4 DANH MỤC TỒN TẠI
        // =====================================================

        private void EnsureFixedCategories()
        {
            bool changed = false;

            foreach (string categoryName in FixedCategoryNames)
            {
                bool exists =
                    db.Categories.Any(
                        c => c.Name == categoryName);

                if (!exists)
                {
                    db.Categories.Add(
                        new Category
                        {
                            Name = categoryName
                        });

                    changed = true;
                }
            }

            if (changed)
            {
                db.SaveChanges();
            }
        }

        // =====================================================
        // ĐẢM BẢO 5 SIZE TỒN TẠI
        // =====================================================

        private void EnsureFixedSizes()
        {
            bool changed = false;

            foreach (string sizeName in FixedSizeNames)
            {
                bool exists =
                    db.Sizes.Any(
                        s => s.Name == sizeName);

                if (!exists)
                {
                    db.Sizes.Add(
                        new Size
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

        // =====================================================
        // LẤY SIZE THEO TÊN
        // =====================================================

        private Size GetSizeByName(string sizeName)
        {
            return db.Sizes
                .FirstOrDefault(
                    s => s.Name == sizeName);
        }

        // =====================================================
        // ĐẢM BẢO SẢN PHẨM CÓ ĐỦ 5 SIZE
        //
        // Nếu sản phẩm chưa có ProductSize:
        // - Lấy Product.Stock cũ
        // - Chia tồn vào 5 size
        //
        // Nếu đã có một số size:
        // - Giữ nguyên tồn kho các size đang có
        // - Chỉ tạo thêm size còn thiếu với tồn = 0
        // =====================================================

        private void EnsureProductHasAllSizes(
            int productId,
            bool distributeOldStockIfNoSize = true)
        {
            var product =
                db.Products
                    .FirstOrDefault(
                        p => p.ProductId == productId);

            if (product == null)
            {
                return;
            }

            EnsureFixedSizes();

            var fixedSizes =
                db.Sizes
                    .Where(s =>
                        FixedSizeNames.Contains(s.Name))
                    .ToList();

            var productSizes =
                db.ProductSizes
                    .Where(
                        ps => ps.ProductId == productId)
                    .ToList();

            // =================================================
            // TRƯỜNG HỢP SẢN PHẨM CHƯA CÓ SIZE NÀO
            // =================================================

            if (!productSizes.Any())
            {
                int oldStock = product.Stock;

                if (oldStock < 0)
                {
                    oldStock = 0;
                }

                int baseStock =
                    oldStock / fixedSizes.Count;

                int remainder =
                    oldStock % fixedSizes.Count;

                for (int i = 0;
                     i < fixedSizes.Count;
                     i++)
                {
                    int stock =
                        baseStock +
                        (i < remainder ? 1 : 0);

                    db.ProductSizes.Add(
                        new ProductSize
                        {
                            ProductId = productId,
                            SizeId = fixedSizes[i].SizeId,
                            Stock = stock
                        });
                }

                db.SaveChanges();

                return;
            }

            // =================================================
            // TRƯỜNG HỢP ĐÃ CÓ MỘT SỐ SIZE
            // =================================================

            bool changed = false;

            foreach (var size in fixedSizes)
            {
                bool exists =
                    productSizes.Any(
                        ps => ps.SizeId == size.SizeId);

                if (!exists)
                {
                    db.ProductSizes.Add(
                        new ProductSize
                        {
                            ProductId = productId,
                            SizeId = size.SizeId,
                            Stock = 0
                        });

                    changed = true;
                }
            }

            if (changed)
            {
                db.SaveChanges();
            }
        }

        // =====================================================
        // CẬP NHẬT TỔNG TỒN SẢN PHẨM
        //
        // Product.Stock = tổng tồn của 5 size
        // =====================================================

        private int SyncProductTotalStock(int productId)
        {
            var product =
                db.Products
                    .FirstOrDefault(
                        p => p.ProductId == productId);

            if (product == null)
            {
                return 0;
            }

            var totalStock =
                db.ProductSizes
                    .Where(
                        ps => ps.ProductId == productId)
                    .Select(
                        ps => (int?)ps.Stock)
                    .Sum() ?? 0;

            product.Stock = totalStock;

            return totalStock;
        }

        // =====================================================
        // CẬP NHẬT TỒN KHO 1 SIZE
        // =====================================================

        private void UpdateProductSizeStock(
            int productId,
            string sizeName,
            int stock)
        {
            if (stock < 0)
            {
                stock = 0;
            }

            var size =
                db.Sizes
                    .FirstOrDefault(
                        s => s.Name == sizeName);

            if (size == null)
            {
                return;
            }

            var productSize =
                db.ProductSizes
                    .FirstOrDefault(
                        ps =>
                            ps.ProductId == productId &&
                            ps.SizeId == size.SizeId);

            if (productSize == null)
            {
                productSize =
                    new ProductSize
                    {
                        ProductId = productId,
                        SizeId = size.SizeId,
                        Stock = stock
                    };

                db.ProductSizes.Add(productSize);
            }
            else
            {
                productSize.Stock = stock;
            }
        }

        // =====================================================
        // LẤY TỒN KHO 1 SIZE
        // =====================================================

        private int GetProductSizeStock(
            int productId,
            string sizeName)
        {
            var productSize =
                db.ProductSizes
                    .Include(ps => ps.Size)
                    .FirstOrDefault(
                        ps =>
                            ps.ProductId == productId &&
                            ps.Size.Name == sizeName);

            if (productSize == null)
            {
                return 0;
            }

            return productSize.Stock;
        }

        // =====================================================
        // DASHBOARD
        // =====================================================

        public ActionResult Index()
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            EnsureFixedCategories();
            EnsureFixedSizes();

            ViewBag.UserCount =
                db.Users.Count(
                    u => u.Role == "Customer");

            ViewBag.ProductCount =
                db.Products.Count();

            ViewBag.CategoryCount =
                FixedCategoryNames.Length;

            ViewBag.OrderCount =
                db.Orders.Count();

            ViewBag.PendingOrderCount =
                db.Orders.Count(
                    o => o.Status == "Pending");

            ViewBag.TotalRevenue =
                db.Orders
                    .Where(o =>
                        o.Status == "Completed" ||
                        o.Status == "Delivered")
                    .Select(
                        o => (decimal?)o.TotalAmount)
                    .Sum() ?? 0;

            return View();
        }

        // =====================================================
        // QUẢN LÝ TÀI KHOẢN
        // =====================================================

        public ActionResult Users()
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var users =
                db.Users
                    .OrderByDescending(
                        u => u.UserId)
                    .ToList();

            return View(users);
        }

        // =====================================================
        // KHÓA / MỞ KHÓA TÀI KHOẢN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleUser(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var user =
                db.Users
                    .FirstOrDefault(
                        u => u.UserId == id);

            if (user == null)
            {
                return HttpNotFound();
            }

            int currentUserId =
                (int)Session["UserId"];

            if (user.UserId == currentUserId)
            {
                TempData["Error"] =
                    "Không thể khóa tài khoản Admin đang đăng nhập.";

                return RedirectToAction("Users");
            }

            user.IsActive =
                !user.IsActive;

            db.SaveChanges();

            TempData["Success"] =
                user.IsActive
                    ? "Đã mở khóa tài khoản."
                    : "Đã khóa tài khoản.";

            return RedirectToAction("Users");
        }

        // =====================================================
        // ĐỔI QUYỀN CUSTOMER / ADMIN
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeRole(
            int id,
            string role)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            if (role != "Admin" &&
                role != "Customer")
            {
                TempData["Error"] =
                    "Quyền tài khoản không hợp lệ.";

                return RedirectToAction("Users");
            }

            var user =
                db.Users
                    .FirstOrDefault(
                        u => u.UserId == id);

            if (user == null)
            {
                return HttpNotFound();
            }

            int currentUserId =
                (int)Session["UserId"];

            if (user.UserId == currentUserId &&
                role != "Admin")
            {
                TempData["Error"] =
                    "Không thể hạ quyền Admin đang đăng nhập.";

                return RedirectToAction("Users");
            }

            user.Role = role;

            db.SaveChanges();

            TempData["Success"] =
                "Đã cập nhật quyền tài khoản.";

            return RedirectToAction("Users");
        }

        // =====================================================
        // QUẢN LÝ SẢN PHẨM
        // =====================================================

        public ActionResult Products(
            string search,
            int? categoryId)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            EnsureFixedCategories();
            EnsureFixedSizes();

            // =================================================
            // LẤY DANH SÁCH SẢN PHẨM
            // =================================================

            var products =
                db.Products
                    .Include(p => p.Category)
                    .AsQueryable();

            // Tìm kiếm: xử lý ở ngoài LINQ-to-Entities
            // để tránh lỗi IsNullOrWhiteSpace không được EF6 hỗ trợ.
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                products = products.Where(
                    p => p.Name.Contains(search));
            }

            if (categoryId.HasValue)
            {
                products = products.Where(
                    p => p.CategoryId == categoryId.Value);
            }

            var productList =
                products
                    .OrderByDescending(
                        p => p.ProductId)
                    .ToList();

            // =================================================
            // ĐẢM BẢO MỖI SẢN PHẨM CÓ ĐỦ 5 SIZE
            // VÀ Product.Stock = TỔNG TỒN CỦA 5 SIZE
            // =================================================

            foreach (var product in productList)
            {
                EnsureProductHasAllSizes(
                    product.ProductId);

                SyncProductTotalStock(
                    product.ProductId);
            }

            db.SaveChanges();

            // =================================================
            // LOAD TỒN KHO THEO TỪNG SIZE CHO VIEW
            // =================================================

            var productIds =
                productList
                    .Select(p => p.ProductId)
                    .ToList();

            var productSizeRows =
                db.ProductSizes
                    .Include(ps => ps.Size)
                    .Where(ps => productIds.Contains(ps.ProductId))
                    .ToList();

            var productSizeStocks =
                new Dictionary<int, Dictionary<string, int>>();

            foreach (var product in productList)
            {
                var sizeStocks =
                    new Dictionary<string, int>();

                foreach (string sizeName in FixedSizeNames)
                {
                    var row =
                        productSizeRows.FirstOrDefault(
                            ps =>
                                ps.ProductId == product.ProductId &&
                                ps.Size != null &&
                                ps.Size.Name == sizeName);

                    sizeStocks[sizeName] =
                        row != null ? row.Stock : 0;
                }

                productSizeStocks[product.ProductId] =
                    sizeStocks;
            }

            ViewBag.ProductSizeStocks =
                productSizeStocks;

            ViewBag.Categories =
                GetFixedCategories();

            ViewBag.CategoryId =
                categoryId;

            ViewBag.Search =
                search;

            return View(productList);
        }

        // =====================================================
        // THÊM SẢN PHẨM - GET
        // =====================================================

        [HttpGet]
        public ActionResult CreateProduct()
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            EnsureFixedCategories();
            EnsureFixedSizes();

            ViewBag.Categories =
                GetFixedCategories();

            return View();
        }

        // =====================================================
        // THÊM SẢN PHẨM - POST
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateProduct(
            Product product)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            EnsureFixedCategories();
            EnsureFixedSizes();

            var validCategory =
                db.Categories.Any(
                    c =>
                        c.CategoryId ==
                        product.CategoryId
                        &&
                        FixedCategoryNames.Contains(
                            c.Name));

            if (!validCategory)
            {
                ModelState.AddModelError(
                    "CategoryId",
                    "Vui lòng chọn một trong 4 danh mục của cửa hàng.");
            }

            if (product.Stock < 0)
            {
                ModelState.AddModelError(
                    "Stock",
                    "Số lượng tồn kho không được âm.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Categories =
                    GetFixedCategories();

                return View(product);
            }

            product.Name =
                product.Name == null
                    ? null
                    : product.Name.Trim();

            product.IsActive = true;

            if (product.Stock < 0)
            {
                product.Stock = 0;
            }

            db.Products.Add(product);

            db.SaveChanges();

            // =================================================
            // TẠO ĐỦ 5 SIZE
            // VÀ PHÂN BỔ TỒN BAN ĐẦU
            // =================================================

            EnsureProductHasAllSizes(
                product.ProductId,
                true);

            SyncProductTotalStock(
                product.ProductId);

            db.SaveChanges();

            TempData["Success"] =
                "Đã thêm sản phẩm và thiết lập tồn kho theo 5 Size thành công.";

            return RedirectToAction("Products");
        }

        // =====================================================
        // SỬA SẢN PHẨM - GET
        // =====================================================

        [HttpGet]
        public ActionResult EditProduct(int? id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            // Nếu truy cập /Admin/EditProduct mà không có ProductId,
            // không để MVC báo lỗi null cho tham số int.
            if (!id.HasValue)
            {
                TempData["Error"] =
                    "Vui lòng chọn sản phẩm cần sửa từ danh sách quản lý sản phẩm.";

                return RedirectToAction("Products");
            }

            EnsureFixedCategories();
            EnsureFixedSizes();

            var product =
                db.Products
                    .Include(p => p.Category)
                    .FirstOrDefault(
                        p => p.ProductId == id.Value);

            if (product == null)
            {
                return HttpNotFound();
            }

            // =================================================
            // TỰ ĐỘNG TẠO ĐỦ 5 SIZE
            // =================================================

            EnsureProductHasAllSizes(
                product.ProductId);

            SyncProductTotalStock(
                product.ProductId);

            db.SaveChanges();

            // =================================================
            // LẤY TỒN TỪNG SIZE
            // =================================================

            ViewBag.Stock_S =
                GetProductSizeStock(
                    product.ProductId,
                    "S");

            ViewBag.Stock_M =
                GetProductSizeStock(
                    product.ProductId,
                    "M");

            ViewBag.Stock_L =
                GetProductSizeStock(
                    product.ProductId,
                    "L");

            ViewBag.Stock_XL =
                GetProductSizeStock(
                    product.ProductId,
                    "XL");

            ViewBag.Stock_2XL =
                GetProductSizeStock(
                    product.ProductId,
                    "2XL");

            ViewBag.TotalStock =
                ViewBag.Stock_S +
                ViewBag.Stock_M +
                ViewBag.Stock_L +
                ViewBag.Stock_XL +
                ViewBag.Stock_2XL;

            ViewBag.Categories =
                GetFixedCategories();

            return View(product);
        }

        // =====================================================
        // SỬA SẢN PHẨM - POST
        //
        // CẬP NHẬT:
        // S
        // M
        // L
        // XL
        // 2XL
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditProduct(
            Product product,
            int stockS = 0,
            int stockM = 0,
            int stockL = 0,
            int stockXL = 0,
            int stock2XL = 0)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            EnsureFixedCategories();
            EnsureFixedSizes();

            // =================================================
            // KHÔNG CHO TỒN ÂM
            // =================================================

            if (stockS < 0)
            {
                ModelState.AddModelError(
                    "stockS",
                    "Tồn kho Size S không được âm.");
            }

            if (stockM < 0)
            {
                ModelState.AddModelError(
                    "stockM",
                    "Tồn kho Size M không được âm.");
            }

            if (stockL < 0)
            {
                ModelState.AddModelError(
                    "stockL",
                    "Tồn kho Size L không được âm.");
            }

            if (stockXL < 0)
            {
                ModelState.AddModelError(
                    "stockXL",
                    "Tồn kho Size XL không được âm.");
            }

            if (stock2XL < 0)
            {
                ModelState.AddModelError(
                    "stock2XL",
                    "Tồn kho Size 2XL không được âm.");
            }

            // =================================================
            // KIỂM TRA DANH MỤC
            // =================================================

            var validCategory =
                db.Categories.Any(
                    c =>
                        c.CategoryId ==
                        product.CategoryId
                        &&
                        FixedCategoryNames.Contains(
                            c.Name));

            if (!validCategory)
            {
                ModelState.AddModelError(
                    "CategoryId",
                    "Danh mục sản phẩm không hợp lệ.");
            }

            // =================================================
            // NẾU CÓ LỖI
            // =================================================

            if (!ModelState.IsValid)
            {
                ViewBag.Categories =
                    GetFixedCategories();

                ViewBag.Stock_S =
                    stockS;

                ViewBag.Stock_M =
                    stockM;

                ViewBag.Stock_L =
                    stockL;

                ViewBag.Stock_XL =
                    stockXL;

                ViewBag.Stock_2XL =
                    stock2XL;

                ViewBag.TotalStock =
                    stockS +
                    stockM +
                    stockL +
                    stockXL +
                    stock2XL;

                return View(product);
            }

            // =================================================
            // LẤY SẢN PHẨM GỐC
            // =================================================

            var oldProduct =
                db.Products
                    .FirstOrDefault(
                        p =>
                            p.ProductId ==
                            product.ProductId);

            if (oldProduct == null)
            {
                return HttpNotFound();
            }

            // =================================================
            // CẬP NHẬT THÔNG TIN SẢN PHẨM
            // =================================================

            oldProduct.Name =
                product.Name == null
                    ? null
                    : product.Name.Trim();

            oldProduct.Description =
                product.Description;

            oldProduct.Price =
                product.Price;

            oldProduct.ImageUrl =
                product.ImageUrl;

            oldProduct.CategoryId =
                product.CategoryId;

            oldProduct.IsActive =
                product.IsActive;

            // =================================================
            // ĐẢM BẢO ĐỦ 5 SIZE
            // =================================================

            EnsureProductHasAllSizes(
                oldProduct.ProductId);

            // =================================================
            // CẬP NHẬT TỪNG SIZE
            // =================================================

            UpdateProductSizeStock(
                oldProduct.ProductId,
                "S",
                stockS);

            UpdateProductSizeStock(
                oldProduct.ProductId,
                "M",
                stockM);

            UpdateProductSizeStock(
                oldProduct.ProductId,
                "L",
                stockL);

            UpdateProductSizeStock(
                oldProduct.ProductId,
                "XL",
                stockXL);

            UpdateProductSizeStock(
                oldProduct.ProductId,
                "2XL",
                stock2XL);

            // =================================================
            // TỔNG TỒN = TỔNG 5 SIZE
            // =================================================

            oldProduct.Stock =
                stockS +
                stockM +
                stockL +
                stockXL +
                stock2XL;

            db.SaveChanges();

            TempData["Success"] =
                "Đã cập nhật sản phẩm và tồn kho từng Size thành công.";

            return RedirectToAction("Products");
        }

        // =====================================================
        // ẨN / HIỆN SẢN PHẨM
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleProduct(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var product =
                db.Products
                    .FirstOrDefault(
                        p => p.ProductId == id);

            if (product == null)
            {
                return HttpNotFound();
            }

            product.IsActive =
                !product.IsActive;

            db.SaveChanges();

            TempData["Success"] =
                product.IsActive
                    ? "Đã hiển thị sản phẩm."
                    : "Đã ẩn sản phẩm.";

            return RedirectToAction("Products");
        }

        // =====================================================
        // QUẢN LÝ SIZE CHO SẢN PHẨM - GET
        // =====================================================

        [HttpGet]
        public ActionResult ManageProductSizes(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            EnsureFixedSizes();

            var product =
                db.Products
                    .FirstOrDefault(
                        p => p.ProductId == id);

            if (product == null)
            {
                return HttpNotFound();
            }

            // =================================================
            // ĐẢM BẢO ĐỦ 5 SIZE
            // =================================================

            EnsureProductHasAllSizes(
                product.ProductId);

            var sizes =
                db.Sizes
                    .Where(s =>
                        FixedSizeNames.Contains(
                            s.Name))
                    .ToList()
                    .OrderBy(
                        s => GetSizeOrder(s.Name))
                    .ToList();

            var selectedSizeIds =
                db.ProductSizes
                    .Where(
                        ps => ps.ProductId == id)
                    .Select(
                        ps => ps.SizeId)
                    .ToList();

            ViewBag.Product =
                product;

            ViewBag.SelectedSizeIds =
                selectedSizeIds;

            return View(sizes);
        }

        // =====================================================
        // LƯU SIZE CHO SẢN PHẨM
        //
        // KHÔNG XÓA TỒN KHO CŨ
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ManageProductSizes(
            int id,
            int[] sizeIds)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            EnsureFixedSizes();

            var product =
                db.Products
                    .FirstOrDefault(
                        p => p.ProductId == id);

            if (product == null)
            {
                return HttpNotFound();
            }

            if (sizeIds == null)
            {
                sizeIds =
                    new int[0];
            }

            var validSizeIds =
                db.Sizes
                    .Where(
                        s =>
                            sizeIds.Contains(
                                s.SizeId)
                            &&
                            FixedSizeNames.Contains(
                                s.Name))
                    .Select(
                        s => s.SizeId)
                    .Distinct()
                    .ToList();

            // =================================================
            // KHÔNG XÓA PRODUCTSIZE CŨ
            //
            // Chỉ tạo thêm Size được chọn.
            // Tồn kho cũ được giữ nguyên.
            // =================================================

            foreach (var sizeId in validSizeIds)
            {
                bool exists =
                    db.ProductSizes.Any(
                        ps =>
                            ps.ProductId == id &&
                            ps.SizeId == sizeId);

                if (!exists)
                {
                    db.ProductSizes.Add(
                        new ProductSize
                        {
                            ProductId = id,
                            SizeId = sizeId,
                            Stock = 0
                        });
                }
            }

            // =================================================
            // CẬP NHẬT TỔNG TỒN
            // =================================================

            db.SaveChanges();

            SyncProductTotalStock(id);

            db.SaveChanges();

            TempData["Success"] =
                "Đã cập nhật Size cho sản phẩm. Tồn kho cũ được giữ nguyên.";

            return RedirectToAction("Products");
        }

        // =====================================================
        // QUẢN LÝ DANH MỤC
        // =====================================================

        public ActionResult Categories()
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            EnsureFixedCategories();

            var categories =
                GetFixedCategories();

            return View(categories);
        }

        // =====================================================
        // KHÔNG CHO THÊM DANH MỤC
        // =====================================================

        [HttpGet]
        public ActionResult CreateCategory()
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            TempData["Error"] =
                "Cửa hàng chỉ sử dụng 4 danh mục cố định: Áo nam, Quần nam, Áo nữ, Quần nữ.";

            return RedirectToAction("Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCategory(
            Category category)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            TempData["Error"] =
                "Không thể thêm danh mục mới. Cửa hàng chỉ sử dụng 4 danh mục cố định.";

            return RedirectToAction("Categories");
        }

        // =====================================================
        // KHÔNG CHO SỬA DANH MỤC
        // =====================================================

        [HttpGet]
        public ActionResult EditCategory(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var category =
                db.Categories
                    .FirstOrDefault(
                        c => c.CategoryId == id);

            if (category == null)
            {
                return HttpNotFound();
            }

            if (!FixedCategoryNames.Contains(
                    category.Name))
            {
                return HttpNotFound();
            }

            TempData["Error"] =
                "Không thể đổi tên danh mục cố định.";

            return RedirectToAction("Categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditCategory(
            Category category)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            TempData["Error"] =
                "Không thể đổi tên danh mục cố định.";

            return RedirectToAction("Categories");
        }

        // =====================================================
        // KHÔNG CHO XÓA 4 DANH MỤC
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCategory(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var category =
                db.Categories
                    .FirstOrDefault(
                        c => c.CategoryId == id);

            if (category == null)
            {
                return HttpNotFound();
            }

            if (FixedCategoryNames.Contains(
                    category.Name))
            {
                TempData["Error"] =
                    "Không thể xóa danh mục cố định của cửa hàng.";

                return RedirectToAction("Categories");
            }

            bool hasProducts =
                db.Products.Any(
                    p => p.CategoryId == id);

            if (hasProducts)
            {
                TempData["Error"] =
                    "Không thể xóa danh mục vì đang có sản phẩm thuộc danh mục này.";

                return RedirectToAction("Categories");
            }

            db.Categories.Remove(
                category);

            db.SaveChanges();

            TempData["Success"] =
                "Đã xóa danh mục thành công.";

            return RedirectToAction("Categories");
        }

        // =====================================================
        // QUẢN LÝ SIZE
        // =====================================================

        public ActionResult Sizes()
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            EnsureFixedSizes();

            var sizes =
                db.Sizes
                    .Where(
                        s =>
                            FixedSizeNames.Contains(
                                s.Name))
                    .ToList()
                    .OrderBy(
                        s => GetSizeOrder(s.Name))
                    .ToList();

            return View(sizes);
        }

        // =====================================================
        // KHÔNG CHO THÊM SIZE KHÁC
        // =====================================================

        [HttpGet]
        public ActionResult CreateSize()
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            TempData["Error"] =
                "Cửa hàng chỉ sử dụng 5 Size: S, M, L, XL, 2XL.";

            return RedirectToAction("Sizes");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateSize(Size size)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            TempData["Error"] =
                "Không thể thêm Size mới. Cửa hàng chỉ sử dụng S, M, L, XL, 2XL.";

            return RedirectToAction("Sizes");
        }

        // =====================================================
        // SỬA SIZE
        // =====================================================

        [HttpGet]
        public ActionResult EditSize(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var size =
                db.Sizes
                    .FirstOrDefault(
                        s => s.SizeId == id);

            if (size == null)
            {
                return HttpNotFound();
            }

            TempData["Error"] =
                "Không thể đổi tên Size cố định.";

            return RedirectToAction("Sizes");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditSize(Size size)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            TempData["Error"] =
                "Không thể đổi tên Size cố định.";

            return RedirectToAction("Sizes");
        }

        // =====================================================
        // XÓA SIZE
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteSize(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var size =
                db.Sizes
                    .FirstOrDefault(
                        s => s.SizeId == id);

            if (size == null)
            {
                return HttpNotFound();
            }

            if (FixedSizeNames.Contains(
                    size.Name))
            {
                TempData["Error"] =
                    "Không thể xóa Size cố định của cửa hàng.";

                return RedirectToAction("Sizes");
            }

            bool isUsed =
                db.ProductSizes.Any(
                    ps => ps.SizeId == id);

            if (isUsed)
            {
                TempData["Error"] =
                    "Không thể xóa Size vì Size này đang được sử dụng cho sản phẩm.";

                return RedirectToAction("Sizes");
            }

            db.Sizes.Remove(size);

            db.SaveChanges();

            TempData["Success"] =
                "Đã xóa Size thành công.";

            return RedirectToAction("Sizes");
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
        // QUẢN LÝ ĐƠN HÀNG
        // =====================================================

        public ActionResult Orders(
            string status = null)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var orders =
                db.Orders
                    .Include("User")
                    .OrderByDescending(
                        o => o.CreatedAt)
                    .AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                orders =
                    orders.Where(
                        o => o.Status == status);
            }

            ViewBag.SelectedStatus =
                status;

            return View(
                orders.ToList());
        }

        // =====================================================
        // CHI TIẾT ĐƠN HÀNG
        // =====================================================

        public ActionResult OrderDetails(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var order =
                db.Orders
                    .Include("User")
                    .Include("OrderDetails.Product")
                    .Include("OrderDetails.Size")
                    .FirstOrDefault(
                        o => o.OrderId == id);

            if (order == null)
            {
                return HttpNotFound();
            }

            return View(order);
        }

        // =====================================================
        // ĐỔI TRẠNG THÁI ĐƠN HÀNG
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeOrderStatus(
            int id,
            string status)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var order =
                db.Orders
                    .FirstOrDefault(
                        o => o.OrderId == id);

            if (order == null)
            {
                return HttpNotFound();
            }

            var allowedStatuses =
                new[]
                {
                    "Pending",
                    "Confirmed",
                    "Shipping",
                    "Completed",
                    "Cancelled"
                };

            if (!allowedStatuses.Contains(
                    status))
            {
                TempData["Error"] =
                    "Trạng thái đơn hàng không hợp lệ.";

                return RedirectToAction(
                    "OrderDetails",
                    new
                    {
                        id = id
                    });
            }

            order.Status =
                status;

            db.SaveChanges();

            TempData["Success"] =
                "Đã cập nhật trạng thái đơn hàng.";

            return RedirectToAction(
                "OrderDetails",
                new
                {
                    id = id
                });
        }

        // =====================================================
        // QUẢN LÝ LIÊN HỆ
        // =====================================================

        public ActionResult Contacts(
            string status = null)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var contacts =
                db.ContactMessages
                    .OrderByDescending(
                        c => c.CreatedAt)
                    .AsQueryable();

            if (status == "Unread")
            {
                contacts =
                    contacts.Where(
                        c => !c.IsRead);
            }
            else if (status == "Read")
            {
                contacts =
                    contacts.Where(
                        c => c.IsRead);
            }

            ViewBag.SelectedStatus =
                status;

            return View(
                contacts.ToList());
        }

        // =====================================================
        // CHI TIẾT LIÊN HỆ
        // =====================================================

        public ActionResult ContactDetails(int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var contact =
                db.ContactMessages
                    .FirstOrDefault(
                        c =>
                            c.ContactMessageId ==
                            id);

            if (contact == null)
            {
                return HttpNotFound();
            }

            if (!contact.IsRead)
            {
                contact.IsRead = true;

                db.SaveChanges();
            }

            return View(contact);
        }

        // =====================================================
        // ĐÁNH DẤU ĐÃ ĐỌC / CHƯA ĐỌC
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleContactRead(
            int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var contact =
                db.ContactMessages
                    .FirstOrDefault(
                        c =>
                            c.ContactMessageId ==
                            id);

            if (contact == null)
            {
                return HttpNotFound();
            }

            contact.IsRead =
                !contact.IsRead;

            db.SaveChanges();

            TempData["Success"] =
                contact.IsRead
                    ? "Đã đánh dấu tin nhắn là đã đọc."
                    : "Đã đánh dấu tin nhắn là chưa đọc.";

            return RedirectToAction(
                "Contacts");
        }

        // =====================================================
        // XÓA LIÊN HỆ
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteContact(
            int id)
        {
            if (!IsAdmin())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            var contact =
                db.ContactMessages
                    .FirstOrDefault(
                        c =>
                            c.ContactMessageId ==
                            id);

            if (contact == null)
            {
                return HttpNotFound();
            }

            db.ContactMessages.Remove(
                contact);

            db.SaveChanges();

            TempData["Success"] =
                "Đã xóa tin nhắn liên hệ.";

            return RedirectToAction(
                "Contacts");
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