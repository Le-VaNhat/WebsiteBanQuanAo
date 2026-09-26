using System;
using System.Web.Mvc;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class ContactController : Controller
    {
        private readonly ThienThaiDbContext db = new ThienThaiDbContext();

        // =====================================================
        // TRANG LIÊN HỆ
        // GIAO DIỆN ĐANG NẰM Ở Views/Home/Contact.cshtml
        // =====================================================

        [HttpGet]
        public ActionResult Index()
        {
            return View("~/Views/Home/Contact.cshtml", new ContactMessage());
        }

        // =====================================================
        // KHÁCH GỬI LIÊN HỆ
        // =====================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Send(ContactMessage model)
        {
            if (!ModelState.IsValid)
            {
                return View(
                    "~/Views/Home/Contact.cshtml",
                    model
                );
            }

            try
            {
                model.CreatedAt = DateTime.Now;
                model.IsRead = false;

                db.ContactMessages.Add(model);
                db.SaveChanges();

                TempData["ContactSuccess"] =
                    "Gửi liên hệ thành công! Cảm ơn bạn đã liên hệ với Thiên Thai Shop.";

                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(
                    "",
                    "Không thể gửi liên hệ. Chi tiết: " + ex.Message
                );

                return View(
                    "~/Views/Home/Contact.cshtml",
                    model
                );
            }
        }

        // =====================================================
        // DISPOSE
        // =====================================================

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