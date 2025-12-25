using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AbbaAPP.Data;
using AbbaAPP.Models;
using System.Text.Json;

namespace AbbaAPP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public OrderController(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateOrder([FromBody] OrderRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Получить User ID из сессии
                var session = _httpContextAccessor.HttpContext?.Session;
                var userIdStr = session?.GetString("UserId");

                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован" });
                }

                if (request.Items == null || request.Items.Count == 0)
                {
                    return BadRequest(new { message = "Корзина пуста" });
                }

                // ЗАГРУЖАЕМ ПОЛЬЗОВАТЕЛЯ С ОПТИМИСТИЧЕСКОЙ БЛОКИРОВКОЙ
                var user = await _context.Users
                    .Where(u => u.Id == userId)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return Unauthorized(new { message = "Пользователь не найден" });
                }

                decimal totalPrice = 0;
                var order = new Order
                {
                    UserId = userId,
                    Status = OrderStatus.Processing,
                    CreatedAt = DateTime.UtcNow
                };

                // СПИСОК ДЛЯ ОБНОВЛЕНИЯ КОЛИЧЕСТВА ТОВАРОВ
                var itemsToUpdate = new List<GameItem>();

                // ПРОВЕРЯЕМ ДОСТУПНОСТЬ ТОВАРОВ ПЕРЕД СОЗДАНИЕМ ЗАКАЗА
                foreach (var cartItem in request.Items)
                {
                    var gameItem = await _context.GameItems
                        .Where(g => g.Id == cartItem.Id)
                        .FirstOrDefaultAsync();

                    if (gameItem == null)
                    {
                        return BadRequest(new { message = $"Товар '{cartItem.Name}' не найден" });
                    }

                    if (gameItem.Quantity < cartItem.Quantity)
                    {
                        return BadRequest(new
                        {
                            message = $"Недостаточно товара: {cartItem.Name}. " +
                                     $"Доступно: {gameItem.Quantity} шт., запрошено: {cartItem.Quantity} шт."
                        });
                    }

                    // УМЕНЬШАЕМ КОЛИЧЕСТВО ТОВАРА В ПАМЯТИ
                    gameItem.Quantity -= cartItem.Quantity;
                    gameItem.UpdatedAt = DateTime.UtcNow;

                    // ОТМЕЧАЕМ ДЛЯ ОБНОВЛЕНИЯ
                    _context.Entry(gameItem).State = EntityState.Modified;

                    decimal itemTotal = gameItem.Price * cartItem.Quantity;
                    totalPrice += itemTotal;

                    var orderItem = new OrderItem
                    {
                        GameItemId = gameItem.Id,
                        Quantity = cartItem.Quantity,
                        Price = gameItem.Price
                    };

                    order.OrderItems.Add(orderItem);
                }

                order.TotalPrice = totalPrice;

                // Проверить баланс
                if (user.Balance < totalPrice)
                {
                    return BadRequest(new
                    {
                        message = $"Недостаточно средств на счёте. " +
                                 $"Требуется: {totalPrice:F2} ₽, доступно: {user.Balance:F2} ₽"
                    });
                }

                // Списать деньги с баланса пользователя
                user.Balance -= totalPrice;
                _context.Entry(user).State = EntityState.Modified;

                // СОЗДАЕМ ЗАКАЗ И СОХРАНЯЕМ В БАЗЕ ДАННЫХ
                await _context.Orders.AddAsync(order);

                // ВАЖНО: Сохраняем изменения для товаров и пользователя
                await _context.SaveChangesAsync();

                // Подтверждаем транзакцию
                await transaction.CommitAsync();

                // Обновить UserData в Session
                var updatedUser = await _context.Users
                    .Where(u => u.Id == userId)
                    .Select(u => new
                    {
                        id = u.Id,
                        username = u.Username,
                        email = u.Email,
                        isAdmin = u.IsAdmin,
                        balance = u.Balance,
                        createdAt = u.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (session != null && updatedUser != null)
                {
                    session.SetString("UserData", JsonSerializer.Serialize(updatedUser));
                }

                return Ok(new
                {
                    message = "Заказ успешно оформлен",
                    balance = updatedUser?.balance ?? user.Balance,
                    order = new
                    {
                        id = order.Id,
                        totalPrice = order.TotalPrice,
                        status = order.Status.ToString(),
                        createdAt = order.CreatedAt,
                        itemsCount = order.OrderItems.Count
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        [HttpGet("user-orders")]
        public async Task<IActionResult> GetUserOrders()
        {
            try
            {
                var session = _httpContextAccessor.HttpContext?.Session;
                var userIdStr = session?.GetString("UserId");

                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован" });
                }

                var orders = await _context.Orders
                    .Where(o => o.UserId == userId)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.GameItem)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return Ok(new
                {
                    orders = orders.Select(o => new
                    {
                        id = o.Id,
                        totalPrice = o.TotalPrice,
                        status = o.Status.ToString(),
                        createdAt = o.CreatedAt,
                        itemsCount = o.OrderItems.Count,
                        items = o.OrderItems.Select(oi => new
                        {
                            id = oi.GameItemId,
                            name = oi.GameItem.Name,
                            quantity = oi.Quantity,
                            price = oi.Price,
                            category = oi.GameItem.Category,
                            rarity = oi.GameItem.Rarity
                        })
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        [HttpGet("order-details/{id}")]
        public async Task<IActionResult> GetOrderDetails(int id)
        {
            try
            {
                var session = _httpContextAccessor.HttpContext?.Session;
                var userIdStr = session?.GetString("UserId");

                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован" });
                }

                var order = await _context.Orders
                    .Where(o => o.Id == id && o.UserId == userId)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.GameItem)
                    .FirstOrDefaultAsync();

                if (order == null)
                {
                    return NotFound(new { message = "Заказ не найден или у вас нет доступа к этому заказу" });
                }

                return Ok(new
                {
                    id = order.Id,
                    totalPrice = order.TotalPrice,
                    status = order.Status.ToString(),
                    createdAt = order.CreatedAt,
                    completedAt = order.CompletedAt,
                    itemsCount = order.OrderItems.Count,
                    items = order.OrderItems.Select(oi => new
                    {
                        id = oi.GameItemId,
                        name = oi.GameItem.Name,
                        description = oi.GameItem.Description,
                        itemCode = oi.GameItem.ItemCode,
                        quantity = oi.Quantity,
                        price = oi.Price,
                        category = oi.GameItem.Category,
                        rarity = oi.GameItem.Rarity,
                        imageUrl = oi.GameItem.ImageUrl,
                        total = oi.Quantity * oi.Price
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }
    }

    public class OrderRequest
    {
        public List<CartItemDto> Items { get; set; }
    }

    public class CartItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
}