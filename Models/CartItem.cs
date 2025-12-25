using System;

namespace AbbaAPP.Models
{
    public class CartItem
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int GameItemId { get; set; }
        public int Quantity { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        public virtual User User { get; set; }
        public virtual GameItem GameItem { get; set; }
    }
}
