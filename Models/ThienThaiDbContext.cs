using System.Data.Entity;
using System.Linq;

namespace ThienThaiShop.Models
{
    public class ThienThaiDbContext : DbContext
    {
        public ThienThaiDbContext()
            : base("ThienThaiConnection")
        {
            Database.SetInitializer(
                new CreateDatabaseIfNotExists<ThienThaiDbContext>()
            );

            AddDefaultSizes();
            CreateDefaultAdmin();
        }

        // =====================================================
        // DATABASE TABLES
        // =====================================================

        public DbSet<ApplicationUser> Users { get; set; }

        public DbSet<Category> Categories { get; set; }

        public DbSet<Product> Products { get; set; }

        public DbSet<Size> Sizes { get; set; }

        public DbSet<ProductSize> ProductSizes { get; set; }

        public DbSet<Cart> Carts { get; set; }

        public DbSet<CartItem> CartItems { get; set; }

        public DbSet<Order> Orders { get; set; }

        public DbSet<OrderDetail> OrderDetails { get; set; }

        public DbSet<Payment> Payments { get; set; }

        // =====================================================
        // QUẢN LÝ TIN NHẮN LIÊN HỆ
        // =====================================================

        public DbSet<ContactMessage> ContactMessages { get; set; }


        // =====================================================
        // TẠO SIZE MẶC ĐỊNH
        // =====================================================

        private void AddDefaultSizes()
        {
            if (!Sizes.Any(s => s.Name == "S"))
            {
                Sizes.Add(new Size
                {
                    Name = "S"
                });
            }

            if (!Sizes.Any(s => s.Name == "M"))
            {
                Sizes.Add(new Size
                {
                    Name = "M"
                });
            }

            if (!Sizes.Any(s => s.Name == "L"))
            {
                Sizes.Add(new Size
                {
                    Name = "L"
                });
            }

            if (!Sizes.Any(s => s.Name == "XL"))
            {
                Sizes.Add(new Size
                {
                    Name = "XL"
                });
            }

            if (!Sizes.Any(s => s.Name == "2XL"))
            {
                Sizes.Add(new Size
                {
                    Name = "2XL"
                });
            }

            SaveChanges();
        }


        // =====================================================
        // TẠO ADMIN MẶC ĐỊNH
        // =====================================================

        private void CreateDefaultAdmin()
        {
            var admin = Users.FirstOrDefault(
                u => u.Email == "admin@gmail.com"
            );

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    FullName = "Quản trị viên",
                    Email = "admin@gmail.com",
                    Password = "admin123",
                    Phone = "0000000000",
                    Address = "Admin",
                    Role = "Admin",
                    CreatedAt = System.DateTime.Now,
                    IsActive = true
                };

                Users.Add(admin);

                SaveChanges();
            }
            else
            {
                bool changed = false;

                if (admin.Role != "Admin")
                {
                    admin.Role = "Admin";
                    changed = true;
                }

                if (!admin.IsActive)
                {
                    admin.IsActive = true;
                    changed = true;
                }

                if (changed)
                {
                    SaveChanges();
                }
            }
        }


        // =====================================================
        // DISPOSE DATABASE
        // =====================================================

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                base.Dispose(disposing);
            }
            else
            {
                base.Dispose(disposing);
            }
        }
    }
}