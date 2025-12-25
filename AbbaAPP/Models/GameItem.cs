
using System;
using System.Collections.Generic;

namespace AbbaAPP.Models
{
    public class GameItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ItemCode { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
        public string Rarity { get; set; } // Common, Rare, Epic, Legendary
        public string Category { get; set; } // Weapon, Armor, Potion, etc
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Новое поле: владелец товара
        public int? UserId { get; set; } // Nullable, если товар системный
        public bool IsUserItem { get; set; } = false; // Флаг, что это пользовательский товар

        // Навигационные свойства
        public virtual User User { get; set; }
        public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    }
}