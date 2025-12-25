using AbbaAPP.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace AbbaAPP.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<GameItem> GameItems { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Конфигурация User
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Конфигурация GameItem
            modelBuilder.Entity<GameItem>()
                .HasIndex(p => p.ItemCode)
                .IsUnique();

            modelBuilder.Entity<GameItem>()
                .Property(p => p.Price)
                .HasPrecision(10, 2);

            // Связь: User -> GameItems
            modelBuilder.Entity<GameItem>()
                .HasOne(g => g.User)
                .WithMany(u => u.GameItems)
                .HasForeignKey(g => g.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Конфигурация CartItem - ВАЖНО: каскадное удаление
            modelBuilder.Entity<CartItem>()
                .HasOne(c => c.User)
                .WithMany(u => u.CartItems)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CartItem>()
                .HasOne(c => c.GameItem)
                .WithMany(p => p.CartItems)
                .HasForeignKey(c => c.GameItemId)
                .OnDelete(DeleteBehavior.Cascade); // Товар удаляется → удаляется из корзины

            // Конфигурация Order
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .Property(o => o.TotalPrice)
                .HasPrecision(10, 2);

            // Конфигурация OrderItem
            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.GameItem)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.GameItemId)
                .OnDelete(DeleteBehavior.Restrict); // Не удаляем товар из истории заказов

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.Price)
                .HasPrecision(10, 2);
        }
    }
}