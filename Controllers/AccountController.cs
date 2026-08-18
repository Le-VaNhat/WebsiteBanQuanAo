using System;
using System.Linq;
using System.Web.Mvc;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly ThienThaiDbContext db = new ThienThaiDbContext();


        // ==================================================
        // ĐĂNG NHẬP - GET
        // ==================================================

        [HttpGet]
        public ActionResult Login()
        {
            // Nếu đã đăng nhập thì chuyển về trang chủ
            if (Session["UserId"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }


        // ==================================================
        // ĐĂNG NHẬP - POST
        // ==================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập đầy đủ email và mật khẩu.";

                return View();
            }


            email = email.Trim();


            // Tìm tài khoản
            var user = db.Users.FirstOrDefault(u =>
                u.Email == email &&
                u.Password == password &&
                u.IsActive);


            // Không tìm thấy tài khoản
            if (user == null)
            {
                ViewBag.Error = "Email hoặc mật khẩu không chính xác.";

                return View();
            }


            // ==================================================
            // LƯU THÔNG TIN ĐĂNG NHẬP VÀO SESSION
            // ==================================================

            Session["UserId"] = user.UserId;
            Session["FullName"] = user.FullName;
            Session["Email"] = user.Email;
            Session["Role"] = user.Role;


            // Chuyển về trang chủ
            return RedirectToAction("Index", "Home");
        }


        // ==================================================
        // ĐĂNG KÝ - GET
        // ==================================================

        [HttpGet]
        public ActionResult Register()
        {
            // Nếu đã đăng nhập
            if (Session["UserId"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }


        // ==================================================
        // ĐĂNG KÝ - POST
        // ==================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(
            string fullName,
            string email,
            string password,
            string confirmPassword,
            string phone,
            string address)
        {
            // Kiểm tra họ tên
            if (string.IsNullOrWhiteSpace(fullName))
            {
                ViewBag.Error = "Vui lòng nhập họ tên.";

                return View();
            }


            // Kiểm tra email
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Vui lòng nhập email.";

                return View();
            }


            // Kiểm tra mật khẩu
            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui lòng nhập mật khẩu.";

                return View();
            }


            // Kiểm tra xác nhận mật khẩu
            if (password != confirmPassword)
            {
                ViewBag.Error = "Mật khẩu xác nhận không khớp.";

                return View();
            }


            fullName = fullName.Trim();
            email = email.Trim();


            // ==================================================
            // KIỂM TRA EMAIL ĐÃ TỒN TẠI
            // ==================================================

            var existingUser = db.Users
                .FirstOrDefault(u => u.Email == email);


            if (existingUser != null)
            {
                ViewBag.Error =
                    "Email này đã được sử dụng. Vui lòng chọn email khác.";

                return View();
            }


            // ==================================================
            // TẠO TÀI KHOẢN
            // ==================================================

            var user = new ApplicationUser
            {
                FullName = fullName,
                Email = email,
                Password = password,
                Phone = phone,
                Address = address,

                // Tài khoản khách hàng mặc định
                Role = "Customer",

                // Tài khoản mới được kích hoạt
                IsActive = true,

                CreatedAt = DateTime.Now
            };


            db.Users.Add(user);

            db.SaveChanges();


            // ==================================================
            // ĐĂNG KÝ THÀNH CÔNG
            // ==================================================

            TempData["Success"] =
                "Đăng ký tài khoản thành công. Vui lòng đăng nhập.";


            return RedirectToAction("Login");
        }


        // ==================================================
        // ĐĂNG XUẤT
        // ==================================================

        public ActionResult Logout()
        {
            Session.Clear();

            Session.Abandon();

            return RedirectToAction("Index", "Home");
        }


        // ==================================================
        // THÔNG TIN TÀI KHOẢN
        // ==================================================

        [HttpGet]
        public ActionResult Profile()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login");
            }


            int userId = (int)Session["UserId"];


            var user = db.Users
                .FirstOrDefault(u => u.UserId == userId);


            if (user == null)
            {
                Session.Clear();

                return RedirectToAction("Login");
            }


            return View(user);
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