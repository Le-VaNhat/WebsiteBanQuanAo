using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class CheckoutController : Controller
    {
        private readonly ThienThaiDbContext db = new ThienThaiDbContext();

        // ==================================================
        // KIỂM TRA ĐĂNG NHẬP
        // ==================================================

        private bool IsLoggedIn()
        {
            return Session["UserId"] != null;
        }

        // ==================================================
        // LẤY USER ID
        // ==================================================

        private int GetUserId()
        {
            return (int)Session["UserId"];
        }

        // ==================================================
        // KIỂM TRA SỐ ĐIỆN THOẠI
        // ==================================================

        private bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return false;
            }

            phone = phone.Trim();

            // Đúng 10 số và bắt đầu bằng số 0
            return Regex.IsMatch(phone, @"^0\d{9}$");
        }

        // ==================================================
        // KIỂM TRA EMAIL GMAIL
        // ==================================================

        private bool IsValidGmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            email = email.Trim();

            return Regex.IsMatch(
                email,
                @"^[A-Za-z0-9._%+-]+@gmail\.com$",
                RegexOptions.IgnoreCase
            );
        }

        // ==================================================
        // TRANG ĐẶT HÀNG
        // ==================================================

        [HttpGet]
        public ActionResult Index()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = GetUserId();

            // ==================================================
            // LẤY THÔNG TIN USER
            // ==================================================

            var user = db.Users
                .FirstOrDefault(u => u.UserId == userId);

            if (user == null)
            {
                Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            // ==================================================
            // LẤY GIỎ HÀNG
            // ==================================================

            var cart = db.Carts
                .FirstOrDefault(c => c.UserId == userId);

            if (cart == null)
            {
                TempData["Error"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index", "Cart");
            }

            // ==================================================
            // LẤY SẢN PHẨM TRONG GIỎ
            // ==================================================

            var cartItems = db.CartItems
                .Where(c => c.CartId == cart.CartId)
                .ToList();

            if (!cartItems.Any())
            {
                TempData["Error"] = "Giỏ hàng đang trống.";
                return RedirectToAction("Index", "Cart");
            }

            // ==================================================
            // GÁN PRODUCT + SIZE
            // ==================================================

            foreach (var item in cartItems)
            {
                item.Product = db.Products
                    .FirstOrDefault(p =>
                        p.ProductId == item.ProductId);

                item.Size = db.Sizes
                    .FirstOrDefault(s =>
                        s.SizeId == item.SizeId);
            }

            // ==================================================
            // GỬI DỮ LIỆU SANG VIEW
            // ==================================================

            ViewBag.User = user;
            ViewBag.CartItems = cartItems;

            // ==================================================
            // TÍNH TỔNG TIỀN
            // ==================================================

            decimal total = 0;

            foreach (var item in cartItems)
            {
                if (item.Product != null)
                {
                    total += item.Product.Price * item.Quantity;
                }
            }

            ViewBag.Total = total;

            return View();
        }

        // ==================================================
        // XÁC NHẬN ĐẶT HÀNG
        // ==================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PlaceOrder(
            string shippingAddress,
            string phone,
            string email,
            string paymentMethod)
        {
            // ==================================================
            // KIỂM TRA ĐĂNG NHẬP
            // ==================================================

            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = GetUserId();

            // ==================================================
            // LẤY USER
            // ==================================================

            var user = db.Users
                .FirstOrDefault(u => u.UserId == userId);

            if (user == null)
            {
                Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            // ==================================================
            // LÀM SẠCH DỮ LIỆU
            // ==================================================

            shippingAddress = shippingAddress == null
                ? ""
                : shippingAddress.Trim();

            phone = phone == null
                ? ""
                : phone.Trim();

            email = email == null
                ? ""
                : email.Trim();

            // ==================================================
            // NẾU FORM KHÔNG GỬI EMAIL
            // THÌ TỰ LẤY EMAIL TỪ TÀI KHOẢN
            // ==================================================

            if (string.IsNullOrWhiteSpace(email))
            {
                email = user.Email;
            }

            // ==================================================
            // KIỂM TRA ĐỊA CHỈ
            // ==================================================

            if (string.IsNullOrWhiteSpace(shippingAddress))
            {
                TempData["Error"] =
                    "Vui lòng nhập địa chỉ giao hàng.";

                return RedirectToAction("Index");
            }

            // ==================================================
            // KIỂM TRA SỐ ĐIỆN THOẠI
            // ==================================================

            if (string.IsNullOrWhiteSpace(phone))
            {
                TempData["Error"] =
                    "Vui lòng nhập số điện thoại.";

                return RedirectToAction("Index");
            }

            if (!IsValidPhone(phone))
            {
                TempData["Error"] =
                    "Số điện thoại không hợp lệ! Vui lòng nhập đúng 10 số và số đầu tiên phải là 0.";

                return RedirectToAction("Index");
            }

            // ==================================================
            // KIỂM TRA EMAIL
            // ==================================================

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Error"] =
                    "Tài khoản chưa có email. Vui lòng cập nhật email.";

                return RedirectToAction("Index");
            }

            if (!IsValidGmail(email))
            {
                TempData["Error"] =
                    "Email không hợp lệ! Vui lòng sử dụng email có dạng @gmail.com.";

                return RedirectToAction("Index");
            }

            // ==================================================
            // PHƯƠNG THỨC THANH TOÁN
            // ==================================================

            if (string.IsNullOrWhiteSpace(paymentMethod))
            {
                paymentMethod = "COD";
            }

            // ==================================================
            // LẤY GIỎ HÀNG
            // ==================================================

            var cart = db.Carts
                .FirstOrDefault(c => c.UserId == userId);

            if (cart == null)
            {
                TempData["Error"] =
                    "Không tìm thấy giỏ hàng.";

                return RedirectToAction("Index", "Cart");
            }

            // ==================================================
            // LẤY CART ITEMS
            // ==================================================

            var cartItems = db.CartItems
                .Where(c => c.CartId == cart.CartId)
                .ToList();

            if (!cartItems.Any())
            {
                TempData["Error"] =
                    "Giỏ hàng đang trống.";

                return RedirectToAction("Index", "Cart");
            }

            // ==================================================
            // KIỂM TRA SẢN PHẨM + SIZE + TỒN KHO
            // ==================================================

            decimal totalAmount = 0;

            foreach (var item in cartItems)
            {
                // --------------------------------------------------
                // Kiểm tra Product
                // --------------------------------------------------

                var product = db.Products
                    .FirstOrDefault(p =>
                        p.ProductId == item.ProductId);

                if (product == null)
                {
                    TempData["Error"] =
                        "Có sản phẩm không còn tồn tại.";

                    return RedirectToAction("Index", "Cart");
                }

                // --------------------------------------------------
                // Kiểm tra số lượng
                // --------------------------------------------------

                if (item.Quantity <= 0)
                {
                    TempData["Error"] =
                        "Số lượng sản phẩm không hợp lệ.";

                    return RedirectToAction("Index", "Cart");
                }

                // --------------------------------------------------
                // QUAN TRỌNG:
                // LẤY TỒN KHO THEO PRODUCT + SIZE
                // --------------------------------------------------

                var productSize = db.ProductSizes
                    .FirstOrDefault(ps =>
                        ps.ProductId == item.ProductId &&
                        ps.SizeId == item.SizeId);

                if (productSize == null)
                {
                    var size = db.Sizes
                        .FirstOrDefault(s =>
                            s.SizeId == item.SizeId);

                    string sizeName =
                        size != null
                            ? size.Name
                            : "không xác định";

                    TempData["Error"] =
                        "Sản phẩm \"" +
                        product.Name +
                        "\" chưa được thiết lập tồn kho cho Size " +
                        sizeName +
                        ".";

                    return RedirectToAction("Index", "Cart");
                }

                // --------------------------------------------------
                // Kiểm tra tồn kho ĐÚNG THEO SIZE
                // --------------------------------------------------

                if (productSize.Stock < item.Quantity)
                {
                    var size = db.Sizes
                        .FirstOrDefault(s =>
                            s.SizeId == item.SizeId);

                    string sizeName =
                        size != null
                            ? size.Name
                            : "không xác định";

                    TempData["Error"] =
                        "Sản phẩm \"" +
                        product.Name +
                        "\" Size " +
                        sizeName +
                        " chỉ còn " +
                        productSize.Stock +
                        " sản phẩm.";

                    return RedirectToAction("Index", "Cart");
                }

                // --------------------------------------------------
                // Tính tổng tiền
                // --------------------------------------------------

                totalAmount +=
                    product.Price * item.Quantity;
            }

            // ==================================================
            // TẠO ORDER
            // ==================================================

            var order = new Order
            {
                UserId = userId,

                ShippingAddress = shippingAddress,

                Phone = phone,

                TotalAmount = totalAmount,

                PaymentMethod = paymentMethod,

                Status = "Pending",

                CreatedAt = DateTime.Now
            };

            db.Orders.Add(order);

            // ==================================================
            // TẠO ORDER DETAIL
            // + TRỪ TỒN KHO THEO SIZE
            // ==================================================

            foreach (var item in cartItems)
            {
                var product = db.Products
                    .FirstOrDefault(p =>
                        p.ProductId == item.ProductId);

                if (product == null)
                {
                    continue;
                }

                // --------------------------------------------------
                // LẤY PRODUCT SIZE
                // --------------------------------------------------

                var productSize = db.ProductSizes
                    .FirstOrDefault(ps =>
                        ps.ProductId == item.ProductId &&
                        ps.SizeId == item.SizeId);

                if (productSize == null)
                {
                    TempData["Error"] =
                        "Không tìm thấy tồn kho theo Size của sản phẩm \"" +
                        product.Name +
                        "\".";

                    return RedirectToAction("Index", "Cart");
                }

                // --------------------------------------------------
                // KIỂM TRA LẠI TỒN KHO TRƯỚC KHI TRỪ
                // --------------------------------------------------

                if (productSize.Stock < item.Quantity)
                {
                    var size = db.Sizes
                        .FirstOrDefault(s =>
                            s.SizeId == item.SizeId);

                    string sizeName =
                        size != null
                            ? size.Name
                            : "không xác định";

                    TempData["Error"] =
                        "Sản phẩm \"" +
                        product.Name +
                        "\" Size " +
                        sizeName +
                        " không còn đủ số lượng.";

                    return RedirectToAction("Index", "Cart");
                }

                // --------------------------------------------------
                // TẠO ORDER DETAIL
                // --------------------------------------------------

                var orderDetail = new OrderDetail
                {
                    Order = order,

                    ProductId = item.ProductId,

                    SizeId = item.SizeId,

                    Quantity = item.Quantity,

                    UnitPrice = product.Price
                };

                db.OrderDetails.Add(orderDetail);

                // ==================================================
                // QUAN TRỌNG:
                // TRỪ TỒN KHO ĐÚNG SIZE
                // ==================================================

                productSize.Stock -= item.Quantity;

                // Không cho âm
                if (productSize.Stock < 0)
                {
                    productSize.Stock = 0;
                }

                // ==================================================
                // ĐỒNG BỘ PRODUCT.STOCK
                // = TỔNG TỒN CỦA TẤT CẢ SIZE
                // ==================================================

                var allProductSizes = db.ProductSizes
                    .Where(ps =>
                        ps.ProductId == item.ProductId)
                    .ToList();

                product.Stock =
                    allProductSizes.Sum(ps => ps.Stock);
            }

            // ==================================================
            // XÓA GIỎ HÀNG
            // ==================================================

            db.CartItems.RemoveRange(cartItems);

            // ==================================================
            // LƯU TOÀN BỘ:
            // ORDER
            // ORDER DETAIL
            // PRODUCT SIZE STOCK
            // PRODUCT STOCK
            // CART
            // ==================================================

            db.SaveChanges();

            // ==================================================
            // THÔNG BÁO ĐẶT HÀNG THÀNH CÔNG
            // ==================================================

            TempData["Success"] =
                "Đặt hàng thành công! Mã đơn hàng: #" +
                order.OrderId;

            return RedirectToAction(
                "Success",
                new
                {
                    id = order.OrderId
                }
            );
        }

        // ==================================================
        // ĐẶT HÀNG THÀNH CÔNG
        // ==================================================

        [HttpGet]
        public ActionResult Success(int id)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = GetUserId();

            // ==================================================
            // LẤY ORDER
            // ==================================================

            var order = db.Orders
                .FirstOrDefault(o =>
                    o.OrderId == id &&
                    o.UserId == userId);

            if (order == null)
            {
                return HttpNotFound();
            }

            // ==================================================
            // LẤY ORDER DETAIL
            // ==================================================

            order.OrderDetails = db.OrderDetails
                .Where(d =>
                    d.OrderId == order.OrderId)
                .ToList();

            // ==================================================
            // GÁN PRODUCT + SIZE
            // ==================================================

            foreach (var detail in order.OrderDetails)
            {
                detail.Product = db.Products
                    .FirstOrDefault(p =>
                        p.ProductId == detail.ProductId);

                detail.Size = db.Sizes
                    .FirstOrDefault(s =>
                        s.SizeId == detail.SizeId);
            }

            return View(order);
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