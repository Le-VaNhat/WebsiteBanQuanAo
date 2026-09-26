using System.Linq;
using System.Web.Mvc;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class CartController : Controller
    {
        private readonly ThienThaiDbContext db = new ThienThaiDbContext();

        // =========================================================
        // KIỂM TRA ĐĂNG NHẬP
        // =========================================================
        private bool IsLoggedIn()
        {
            return Session["UserId"] != null;
        }

        // =========================================================
        // LẤY USER ID
        // =========================================================
        private int GetUserId()
        {
            return (int)Session["UserId"];
        }

        // =========================================================
        // TỰ ĐỘNG TẠO SIZE
        // =========================================================
        private void EnsureSizes()
        {
            string[] defaultSizes =
            {
                "S",
                "M",
                "L",
                "XL",
                "2XL"
            };

            bool changed = false;

            foreach (var sizeName in defaultSizes)
            {
                var size = db.Sizes
                    .FirstOrDefault(s => s.Name == sizeName);

                if (size == null)
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

        // =========================================================
        // XEM GIỎ HÀNG
        // =========================================================
        [HttpGet]
        public ActionResult Index()
        {
            EnsureSizes();

            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = GetUserId();

            var cart = db.Carts
                .FirstOrDefault(c => c.UserId == userId);

            // Nếu chưa có giỏ hàng
            if (cart == null)
            {
                return View(new Cart
                {
                    UserId = userId
                });
            }

            // Lấy danh sách sản phẩm trong giỏ
            var cartItems = db.CartItems
                .Where(c => c.CartId == cart.CartId)
                .ToList();

            // Load Product + Size
            foreach (var item in cartItems)
            {
                item.Product = db.Products
                    .FirstOrDefault(p =>
                        p.ProductId == item.ProductId);

                item.Size = db.Sizes
                    .FirstOrDefault(s =>
                        s.SizeId == item.SizeId);
            }

            cart.CartItems = cartItems;

            return View(cart);
        }

        // =========================================================
        // THÊM SẢN PHẨM VÀO GIỎ
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddToCart(
            int productId,
            int sizeId,
            int quantity = 1)
        {
            EnsureSizes();

            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            int userId = GetUserId();

            // -----------------------------------------------------
            // KIỂM TRA SẢN PHẨM
            // -----------------------------------------------------
            var product = db.Products
                .FirstOrDefault(p =>
                    p.ProductId == productId &&
                    p.IsActive);

            if (product == null)
            {
                TempData["Error"] =
                    "Sản phẩm không tồn tại hoặc đã ngừng bán.";

                return RedirectToAction(
                    "Index",
                    "Product");
            }

            // -----------------------------------------------------
            // KIỂM TRA SIZE
            // -----------------------------------------------------
            var size = db.Sizes
                .FirstOrDefault(s =>
                    s.SizeId == sizeId);

            if (size == null)
            {
                TempData["Error"] =
                    "Size sản phẩm không hợp lệ.";

                return RedirectToAction(
                    "Details",
                    "Product",
                    new
                    {
                        id = productId
                    });
            }

            // -----------------------------------------------------
            // KIỂM TRA SỐ LƯỢNG
            // -----------------------------------------------------
            if (product.Stock <= 0)
            {
                TempData["Error"] =
                    "Sản phẩm hiện đã hết hàng.";

                return RedirectToAction(
                    "Details",
                    "Product",
                    new
                    {
                        id = productId
                    });
            }

            if (quantity < 1)
            {
                quantity = 1;
            }

            if (quantity > product.Stock)
            {
                quantity = product.Stock;
            }

            // -----------------------------------------------------
            // TÌM HOẶC TẠO GIỎ HÀNG
            // -----------------------------------------------------
            var cart = db.Carts
                .FirstOrDefault(c =>
                    c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId
                };

                db.Carts.Add(cart);

                db.SaveChanges();
            }

            // -----------------------------------------------------
            // KIỂM TRA SẢN PHẨM + SIZE ĐÃ CÓ TRONG GIỎ CHƯA
            // -----------------------------------------------------
            var cartItem = db.CartItems
                .FirstOrDefault(c =>
                    c.CartId == cart.CartId &&
                    c.ProductId == productId &&
                    c.SizeId == sizeId);

            if (cartItem != null)
            {
                // Đã có -> cộng số lượng
                cartItem.Quantity += quantity;

                if (cartItem.Quantity > product.Stock)
                {
                    cartItem.Quantity = product.Stock;
                }
            }
            else
            {
                // Chưa có -> tạo mới
                cartItem = new CartItem
                {
                    CartId = cart.CartId,
                    ProductId = productId,
                    SizeId = sizeId,
                    Quantity = quantity
                };

                db.CartItems.Add(cartItem);
            }

            db.SaveChanges();

            TempData["Success"] =
                "Đã thêm sản phẩm vào giỏ hàng.";

            return RedirectToAction("Index");
        }

        // =========================================================
        // CẬP NHẬT SỐ LƯỢNG
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateQuantity(
            int cartItemId,
            int quantity)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            int userId = GetUserId();

            // Chỉ được sửa item thuộc giỏ của chính mình
            var cartItem = (
                from item in db.CartItems
                join cart in db.Carts
                    on item.CartId equals cart.CartId
                where item.CartItemId == cartItemId
                      && cart.UserId == userId
                select item
            ).FirstOrDefault();

            if (cartItem == null)
            {
                return HttpNotFound();
            }

            var product = db.Products
                .FirstOrDefault(p =>
                    p.ProductId == cartItem.ProductId);

            if (product == null)
            {
                return HttpNotFound();
            }

            if (product.Stock <= 0)
            {
                cartItem.Quantity = 0;
            }
            else
            {
                if (quantity < 1)
                {
                    quantity = 1;
                }

                if (quantity > product.Stock)
                {
                    quantity = product.Stock;
                }

                cartItem.Quantity = quantity;
            }

            db.SaveChanges();

            return RedirectToAction("Index");
        }

        // =========================================================
        // XÓA MỘT SẢN PHẨM
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Remove(int cartItemId)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            int userId = GetUserId();

            // Chỉ xóa item thuộc giỏ của user hiện tại
            var cartItem = (
                from item in db.CartItems
                join cart in db.Carts
                    on item.CartId equals cart.CartId
                where item.CartItemId == cartItemId
                      && cart.UserId == userId
                select item
            ).FirstOrDefault();

            if (cartItem == null)
            {
                return HttpNotFound();
            }

            db.CartItems.Remove(cartItem);

            db.SaveChanges();

            TempData["Success"] =
                "Đã xóa sản phẩm khỏi giỏ hàng.";

            return RedirectToAction("Index");
        }

        // =========================================================
        // XÓA TOÀN BỘ GIỎ HÀNG
        // =========================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Clear()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            int userId = GetUserId();

            var cart = db.Carts
                .FirstOrDefault(c =>
                    c.UserId == userId);

            if (cart != null)
            {
                var items = db.CartItems
                    .Where(c =>
                        c.CartId == cart.CartId)
                    .ToList();

                if (items.Any())
                {
                    db.CartItems.RemoveRange(items);

                    db.SaveChanges();
                }
            }

            TempData["Success"] =
                "Đã xóa toàn bộ giỏ hàng.";

            return RedirectToAction("Index");
        }

        // =========================================================
        // ĐI ĐẾN TRANG THANH TOÁN
        // =========================================================
        [HttpGet]
        public ActionResult Checkout()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account");
            }

            int userId = GetUserId();

            var cart = db.Carts
                .FirstOrDefault(c =>
                    c.UserId == userId);

            if (cart == null)
            {
                TempData["Error"] =
                    "Giỏ hàng đang trống.";

                return RedirectToAction("Index");
            }

            var items = db.CartItems
                .Where(c =>
                    c.CartId == cart.CartId)
                .ToList();

            if (!items.Any())
            {
                TempData["Error"] =
                    "Giỏ hàng đang trống.";

                return RedirectToAction("Index");
            }

            foreach (var item in items)
            {
                item.Product = db.Products
                    .FirstOrDefault(p =>
                        p.ProductId == item.ProductId);

                item.Size = db.Sizes
                    .FirstOrDefault(s =>
                        s.SizeId == item.SizeId);
            }

            cart.CartItems = items;

            return View(cart);
        }

        // =========================================================
        // GIẢI PHÓNG DATABASE
        // =========================================================
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