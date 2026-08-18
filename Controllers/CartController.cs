using System.Linq;
using System.Web.Mvc;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class CartController : Controller
    {
        private readonly ThienThaiDbContext db = new ThienThaiDbContext();


        // ==================================================
        // TỰ ĐỘNG TẠO SIZE S, M, L, XL, 2XL
        // ==================================================
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
                }
            }

            db.SaveChanges();
        }


        // ==================================================
        // XEM GIỎ HÀNG
        // ==================================================
        public ActionResult Index()
        {
            EnsureSizes();

            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = (int)Session["UserId"];

            var cart = db.Carts
                .FirstOrDefault(c => c.UserId == userId);

            if (cart == null)
            {
                return View(new Cart());
            }

            cart.CartItems = db.CartItems
                .Where(c => c.CartId == cart.CartId)
                .ToList();

            foreach (var item in cart.CartItems)
            {
                item.Product = db.Products
                    .FirstOrDefault(p => p.ProductId == item.ProductId);

                item.Size = db.Sizes
                    .FirstOrDefault(s => s.SizeId == item.SizeId);
            }

            return View(cart);
        }


        // ==================================================
        // THÊM SẢN PHẨM VÀO GIỎ
        // ==================================================
        [HttpPost]
        public ActionResult AddToCart(
            int productId,
            int sizeId,
            int quantity = 1)
        {
            EnsureSizes();

            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = (int)Session["UserId"];

            var product = db.Products
                .FirstOrDefault(p =>
                    p.ProductId == productId &&
                    p.IsActive);

            if (product == null)
            {
                return HttpNotFound();
            }

            // Kiểm tra size có tồn tại không
            var size = db.Sizes
                .FirstOrDefault(s => s.SizeId == sizeId);

            if (size == null)
            {
                TempData["Error"] = "Size không hợp lệ.";

                return RedirectToAction(
                    "Details",
                    "Product",
                    new { id = productId }
                );
            }

            if (quantity < 1)
            {
                quantity = 1;
            }

            if (quantity > product.Stock)
            {
                quantity = product.Stock;
            }


            // TÌM HOẶC TẠO GIỎ HÀNG
            var cart = db.Carts
                .FirstOrDefault(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId
                };

                db.Carts.Add(cart);

                db.SaveChanges();
            }


            // KIỂM TRA SẢN PHẨM + SIZE
            var cartItem = db.CartItems
                .FirstOrDefault(c =>
                    c.CartId == cart.CartId &&
                    c.ProductId == productId &&
                    c.SizeId == sizeId);


            if (cartItem != null)
            {
                cartItem.Quantity += quantity;

                if (cartItem.Quantity > product.Stock)
                {
                    cartItem.Quantity = product.Stock;
                }
            }
            else
            {
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

            return RedirectToAction("Index");
        }


        // ==================================================
        // CẬP NHẬT SỐ LƯỢNG
        // ==================================================
        [HttpPost]
        public ActionResult UpdateQuantity(
            int cartItemId,
            int quantity)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (quantity < 1)
            {
                quantity = 1;
            }

            var cartItem = db.CartItems
                .FirstOrDefault(c =>
                    c.CartItemId == cartItemId);

            if (cartItem == null)
            {
                return HttpNotFound();
            }

            var product = db.Products
                .FirstOrDefault(p =>
                    p.ProductId == cartItem.ProductId);

            if (product != null &&
                quantity > product.Stock)
            {
                quantity = product.Stock;
            }

            cartItem.Quantity = quantity;

            db.SaveChanges();

            return RedirectToAction("Index");
        }


        // ==================================================
        // XÓA SẢN PHẨM KHỎI GIỎ
        // ==================================================
        [HttpPost]
        public ActionResult Remove(int cartItemId)
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var cartItem = db.CartItems
                .FirstOrDefault(c =>
                    c.CartItemId == cartItemId);

            if (cartItem == null)
            {
                return HttpNotFound();
            }

            db.CartItems.Remove(cartItem);

            db.SaveChanges();

            return RedirectToAction("Index");
        }


        // ==================================================
        // XÓA TOÀN BỘ GIỎ HÀNG
        // ==================================================
        [HttpPost]
        public ActionResult Clear()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = (int)Session["UserId"];

            var cart = db.Carts
                .FirstOrDefault(c =>
                    c.UserId == userId);

            if (cart != null)
            {
                var items = db.CartItems
                    .Where(c =>
                        c.CartId == cart.CartId)
                    .ToList();

                db.CartItems.RemoveRange(items);

                db.SaveChanges();
            }

            return RedirectToAction("Index");
        }


        // ==================================================
        // GIẢI PHÓNG DATABASE
        // ==================================================
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