using System;
using System.Linq;
using System.Web.Mvc;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly ThienThaiDbContext db = new ThienThaiDbContext();

        // ================================
        // LOGIN - GET
        // ================================

        [HttpGet]
        public ActionResult Login()
        {
            if (Session["UserId"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // ================================
        // LOGIN - POST
        // ================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui long nhap day du email va mat khau.";
                return View();
            }

            email = email.Trim();

            var user = db.Users.FirstOrDefault(u =>
                u.Email == email &&
                u.Password == password &&
                u.IsActive);

            if (user == null)
            {
                ViewBag.Error = "Email hoac mat khau khong chinh xac.";
                return View();
            }

            // Save login information
            Session["UserId"] = user.UserId;
            Session["FullName"] = user.FullName;
            Session["Email"] = user.Email;
            Session["Role"] = user.Role;

            // Admin
            if (!string.IsNullOrEmpty(user.Role) &&
                user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction("Index", "Admin");
            }

            // Customer
            return RedirectToAction("Index", "Home");
        }

        // ================================
        // REGISTER - GET
        // ================================

        [HttpGet]
        public ActionResult Register()
        {
            if (Session["UserId"] != null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // ================================
        // REGISTER - POST
        // ================================

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
            // Check full name
            if (string.IsNullOrWhiteSpace(fullName))
            {
                ViewBag.Error = "Vui long nhap ho ten.";
                return View();
            }

            // Check email
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Vui long nhap email.";
                return View();
            }

            // Check password
            if (string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Vui long nhap mat khau.";
                return View();
            }

            // Check confirm password
            if (password != confirmPassword)
            {
                ViewBag.Error = "Mat khau xac nhan khong khop.";
                return View();
            }

            fullName = fullName.Trim();
            email = email.Trim();

            // Check existing email
            var existingUser = db.Users
                .FirstOrDefault(u => u.Email == email);

            if (existingUser != null)
            {
                ViewBag.Error =
                    "Email nay da duoc su dung. Vui long chon email khac.";

                return View();
            }

            // Create new user
            var user = new ApplicationUser
            {
                FullName = fullName,
                Email = email,
                Password = password,
                Phone = phone,
                Address = address,
                Role = "Customer",
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            db.Users.Add(user);
            db.SaveChanges();

            TempData["Success"] =
                "Dang ky tai khoan thanh cong. Vui long dang nhap.";

            return RedirectToAction("Login");
        }

        // ================================
        // LOGOUT
        // ================================

        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();

            return RedirectToAction("Index", "Home");
        }

        // ================================
        // PROFILE
        // ================================

        [HttpGet]
        public new ActionResult Profile()
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

        // ================================
        // DISPOSE DATABASE
        // ================================

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