using System;

namespace AbbaAPP.Models
{
    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int GameItemId { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }

        // Навигационные свойства
        public virtual Order Order { get; set; }
        public virtual GameItem GameItem { get; set; }
    }

    public enum OrderStatus
    {
        Pending,
        Processing,
        Shipped,
        Delivered,
        Cancelled
    }
}
