using System.Linq;
using System.Net;
using System.Web.Mvc;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class ProductController : Controller
    {
        private readonly ThienThaiDbContext db = new ThienThaiDbContext();

        // ===============================
        // DANH SÁCH SẢN PHẨM
        // ===============================

        public ActionResult Index(string search, string category)
        {
            var products = db.Products
                .Include("Category")
                .Where(p => p.IsActive)
                .AsQueryable();

            // TÌM KIẾM
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                products = products.Where(p =>
                    p.Name.Contains(search) ||
                    (p.Description != null &&
                     p.Description.Contains(search))
                );
            }

            // LỌC DANH MỤC
            if (!string.IsNullOrWhiteSpace(category))
            {
                category = category.Trim();

                products = products.Where(p =>
                    p.Category != null &&
                    p.Category.Name == category
                );
            }

            var productList = products
                .OrderByDescending(p => p.ProductId)
                .ToList();

            ViewBag.Search = search;
            ViewBag.Category = category;

            return View(productList);
        }


        // ===============================
        // SẢN PHẨM BÁN CHẠY
        // ===============================

        public ActionResult BestSeller()
        {
            var products = db.Products
                .Include("Category")
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.Stock)
                .ThenByDescending(p => p.ProductId)
                .Take(20)
                .ToList();

            return View(products);
        }


        // ===============================
        // CHI TIẾT SẢN PHẨM
        // ===============================

        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(
                    HttpStatusCode.BadRequest
                );
            }

            var product = db.Products
                .Include("Category")
                .FirstOrDefault(p => p.ProductId == id);

            if (product == null)
            {
                return HttpNotFound();
            }

            // =========================================
            // TỰ ĐỘNG TẠO 5 SIZE NẾU DATABASE CHƯA CÓ
            // =========================================

            string[] defaultSizes =
            {
                "S",
                "M",
                "L",
                "XL",
                "2XL"
            };

            bool hasNewSize = false;

            foreach (string sizeName in defaultSizes)
            {
                bool exists = db.Sizes.Any(s => s.Name == sizeName);

                if (!exists)
                {
                    db.Sizes.Add(new Size
                    {
                        Name = sizeName
                    });

                    hasNewSize = true;
                }
            }

            if (hasNewSize)
            {
                db.SaveChanges();
            }

            // GỬI 5 SIZE SANG VIEW

            ViewBag.Sizes = db.Sizes
                .Where(s =>
                    s.Name == "S" ||
                    s.Name == "M" ||
                    s.Name == "L" ||
                    s.Name == "XL" ||
                    s.Name == "2XL"
                )
                .OrderBy(s =>
                    s.Name == "S" ? 1 :
                    s.Name == "M" ? 2 :
                    s.Name == "L" ? 3 :
                    s.Name == "XL" ? 4 : 5
                )
                .ToList();

            return View(product);
        }


        // ===============================
        // GIẢI PHÓNG DATABASE
        // ===============================

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