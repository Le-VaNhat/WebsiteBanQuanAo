using System.Linq;
using System.Web.Mvc;
using ThienThaiShop.Models;

namespace ThienThaiShop.Controllers
{
    public class OrderController : Controller
    {
        private readonly ThienThaiDbContext db =
            new ThienThaiDbContext();


        // ==================================================
        // KI?M TRA �ANG NH?P
        // ==================================================

        private bool IsLoggedIn()
        {
            return Session["UserId"] != null;
        }


        // ==================================================
        // L?Y USER ID
        // ==================================================

        private int GetUserId()
        {
            return (int)Session["UserId"];
        }


        // ==================================================
        // DANH S�CH �ON H�NG
        // ==================================================

        [HttpGet]
        public ActionResult Index()
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            int userId = GetUserId();


            var orders = db.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToList();


            return View(orders);
        }


        // ==================================================
        // CHI TI?T �ON H�NG
        // ==================================================

        [HttpGet]
        public ActionResult Details(int id)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            int userId = GetUserId();


            // Ch? cho ph�p xem don h�ng c?a ch�nh m�nh
            var order = db.Orders
                .FirstOrDefault(o =>
                    o.OrderId == id &&
                    o.UserId == userId);


            if (order == null)
            {
                return HttpNotFound();
            }


            // L?y chi ti?t don h�ng
            order.OrderDetails = db.OrderDetails
                .Where(d =>
                    d.OrderId == order.OrderId)
                .ToList();


            // L?y Product v� Size
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
        // H?Y �ON H�NG
        // ==================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            if (!IsLoggedIn())
            {
                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }


            int userId = GetUserId();


            var order = db.Orders
                .FirstOrDefault(o =>
                    o.OrderId == id &&
                    o.UserId == userId);


            if (order == null)
            {
                return HttpNotFound();
            }


            // Ch? cho h?y khi don dang ch? x? l�
            if (order.Status == "Pending")
            {
                order.Status = "Cancelled";


                // Ho�n l?i s? lu?ng s?n ph?m
                var details = db.OrderDetails
                    .Where(d =>
                        d.OrderId == order.OrderId)
                    .ToList();


                foreach (var detail in details)
                {
                    var product = db.Products
                        .FirstOrDefault(p =>
                            p.ProductId == detail.ProductId);


                    if (product != null)
                    {
                        product.Stock += detail.Quantity;
                    }
                }


                db.SaveChanges();


                TempData["Success"] =
                    "�� h?y don h�ng th�nh c�ng.";
            }
            else
            {
                TempData["Error"] =
                    "�on h�ng n�y kh�ng th? h?y.";
            }


            return RedirectToAction(
                "Details",
                new { id = id }
            );
        }


        // ==================================================
        // GI?I PH�NG DATABASE
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