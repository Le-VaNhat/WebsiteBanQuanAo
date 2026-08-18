using System.Data.Entity;
using System.Linq;

namespace ThienThaiShop.Models
{
    public class ThienThaiDbContext : DbContext
    {
        public ThienThaiDbContext()
            : base("ThienThaiConnection")
        {
            // Tự tạo database nếu chưa có
            Database.SetInitializer(
                new CreateDatabaseIfNotExists<ThienThaiDbContext>()
            );

            // Tự thêm 5 size nếu database chưa có
            AddDefaultSizes();
        }


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


        // ==========================================
        // TỰ ĐỘNG THÊM SIZE MẶC ĐỊNH
        // ==========================================

        private void AddDefaultSizes()
        {
            // Kiểm tra từng size, chưa có thì thêm

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
    }
}