using System.Linq;
using System.Web.Mvc;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ThienThaiDbContext db = new ThienThaiDbContext();

        public ActionResult Index()
        {
            // Lấy sản phẩm đang hoạt động, mới nhất trước
            var products = db.Products
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.ProductId)
                .Take(8)
                .ToList();

            // Lấy tất cả danh mục
            var categories = db.Categories
                .OrderBy(c => c.Name)
                .ToList();

            // Gửi dữ liệu sang View
            ViewBag.Products = products;
            ViewBag.Categories = categories;

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Thiên Thai Shop - Thời trang nam nữ";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Liên hệ Thiên Thai Shop";

            return View();
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