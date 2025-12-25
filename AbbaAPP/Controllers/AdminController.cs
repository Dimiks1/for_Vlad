using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AbbaAPP.Data;
using AbbaAPP.Models;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace AbbaAPP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AdminController(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // Проверка администратора
        private bool IsAdmin()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            var userDataJson = session?.GetString("UserData");
            if (!string.IsNullOrEmpty(userDataJson))
            {
                try
                {
                    var userData = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(userDataJson);
                    if (userData.TryGetProperty("isAdmin", out var isAdminProperty) &&
                        isAdminProperty.ValueKind == System.Text.Json.JsonValueKind.True)
                    {
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        // ========== ПОЛЬЗОВАТЕЛИ ==========

        // Получить всех пользователей
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            if (!IsAdmin())
            {
                return Unauthorized(new { message = "Требуются права администратора" });
            }

            try
            {
                var users = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .ToListAsync();

                return Ok(new
                {
                    users = users.Select(u => new
                    {
                        id = u.Id,
                        username = u.Username,
                        email = u.Email,
                        passwordHash = u.PasswordHash,
                        avatarUrl = u.AvatarUrl,
                        balance = u.Balance,
                        isAdmin = u.IsAdmin,
                        createdAt = u.CreatedAt,
                        lastLogin = u.LastLogin
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Получить детали пользователя
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserDetails(int id)
        {
            if (!IsAdmin())
            {
                return Unauthorized(new { message = "Требуются права администратора" });
            }

            try
            {
                var user = await _context.Users
                    .Include(u => u.Orders)
                    .Include(u => u.GameItems)
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (user == null)
                {
                    return NotFound(new { message = "Пользователь не найден" });
                }

                var orders = await _context.Orders
                    .Where(o => o.UserId == id)
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.GameItem)
                    .ToListAsync();

                return Ok(new
                {
                    user = new
                    {
                        id = user.Id,
                        username = user.Username,
                        email = user.Email,
                        passwordHash = user.PasswordHash,
                        avatarUrl = user.AvatarUrl,
                        balance = user.Balance,
                        isAdmin = user.IsAdmin,
                        createdAt = user.CreatedAt,
                        lastLogin = user.LastLogin
                    },
                    stats = new
                    {
                        totalOrders = user.Orders.Count,
                        totalSpent = user.Orders.Sum(o => o.TotalPrice),
                        totalItems = user.GameItems.Count,
                        activeItems = user.GameItems.Count(g => g.Quantity > 0)
                    },
                    recentOrders = orders.Take(5).Select(o => new
                    {
                        id = o.Id,
                        totalPrice = o.TotalPrice,
                        status = o.Status.ToString(),
                        createdAt = o.CreatedAt,
                        itemCount = o.OrderItems.Count
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Пополнить баланс пользователя
        [HttpPost("users/{id}/balance")]
        public async Task<IActionResult> UpdateUserBalance(int id, [FromBody] UpdateBalanceRequest request)
        {
            if (!IsAdmin())
            {
                return Unauthorized(new { message = "Требуются права администратора" });
            }

            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "Пользователь не найден" });
                }

                var adminSession = _httpContextAccessor.HttpContext?.Session;
                var adminUserIdStr = adminSession?.GetString("UserId");
                int.TryParse(adminUserIdStr, out int adminUserId);

                var adminUser = await _context.Users.FindAsync(adminUserId);

                // Логируем операцию (можно сохранить в отдельной таблице транзакций)
                user.Balance += request.Amount;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Баланс пользователя {user.Username} успешно пополнен на {request.Amount} ₽",
                    newBalance = user.Balance,
                    operation = new
                    {
                        admin = adminUser?.Username ?? "Система",
                        amount = request.Amount,
                        timestamp = DateTime.UtcNow,
                        previousBalance = user.Balance - request.Amount,
                        newBalance = user.Balance
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Сделать пользователя администратором
        [HttpPost("users/{id}/make-admin")]
        public async Task<IActionResult> MakeUserAdmin(int id)
        {
            if (!IsAdmin())
            {
                return Unauthorized(new { message = "Требуются права администратора" });
            }

            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "Пользователь не найден" });
                }

                user.IsAdmin = true;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Пользователь {user.Username} теперь администратор"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Сбросить пароль пользователя
        [HttpPost("users/{id}/reset-password")]
        public async Task<IActionResult> ResetUserPassword(int id, [FromBody] ResetPasswordRequest request)
        {
            if (!IsAdmin())
            {
                return Unauthorized(new { message = "Требуются права администратора" });
            }

            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "Пользователь не найден" });
                }

                // Хэшируем новый пароль
                user.PasswordHash = HashPassword(request.NewPassword);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = $"Пароль пользователя {user.Username} успешно сброшен",
                    newPassword = request.NewPassword // Осторожно! Только для админа в ответе API
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // ========== ТОВАРЫ ==========

        // Получить все товары (включая системные)
        [HttpGet("items")]
        public async Task<IActionResult> GetAllItems()
        {
            if (!IsAdmin())
            {
                return Unauthorized(new { message = "Требуются права администратора" });
            }

            try
            {
                var items = await _context.GameItems
                    .Include(g => g.User)
                    .OrderByDescending(g => g.CreatedAt)
                    .ToListAsync();

                return Ok(new
                {
                    items = items.Select(g => new
                    {
                        id = g.Id,
                        name = g.Name,
                        description = g.Description,
                        itemCode = g.ItemCode,
                        price = g.Price,
                        quantity = g.Quantity,
                        imageUrl = g.ImageUrl,
                        rarity = g.Rarity,
                        category = g.Category,
                        createdAt = g.CreatedAt,
                        updatedAt = g.UpdatedAt,
                        isUserItem = g.IsUserItem,
                        userId = g.UserId,
                        userName = g.User != null ? g.User.Username : "Система"
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Получить детали товара
        [HttpGet("items/{id}")]
        public async Task<IActionResult> GetItemDetails(int id)
        {
            if (!IsAdmin())
            {
                return Unauthorized(new { message = "Требуются права администратора" });
            }

            try
            {
                var item = await _context.GameItems
                    .Include(g => g.User)
                    .Include(g => g.CartItems)
                    .Include(g => g.OrderItems)
                    .ThenInclude(oi => oi.Order)
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (item == null)
                {
                    return NotFound(new { message = "Товар не найден" });
                }

                var inCarts = item.CartItems.Select(c => c.UserId).Distinct().ToList();
                var inOrders = item.OrderItems.Select(oi => oi.OrderId).Distinct().ToList();

                return Ok(new
                {
                    item = new
                    {
                        id = item.Id,
                        name = item.Name,
                        description = item.Description,
                        itemCode = item.ItemCode,
                        price = item.Price,
                        quantity = item.Quantity,
                        imageUrl = item.ImageUrl,
                        rarity = item.Rarity,
                        category = item.Category,
                        createdAt = item.CreatedAt,
                        updatedAt = item.UpdatedAt,
                        isUserItem = item.IsUserItem,
                        userId = item.UserId,
                        userName = item.User != null ? item.User.Username : "Система"
                    },
                    stats = new
                    {
                        inCartsCount = inCarts.Count,
                        inOrdersCount = inOrders.Count,
                        totalSold = item.OrderItems.Sum(oi => oi.Quantity),
                        totalRevenue = item.OrderItems.Sum(oi => oi.Quantity * oi.Price)
                    },
                    recentOrders = item.OrderItems
                        .OrderByDescending(oi => oi.Order.CreatedAt)
                        .Take(5)
                        .Select(oi => new
                        {
                            orderId = oi.OrderId,
                            quantity = oi.Quantity,
                            price = oi.Price,
                            orderDate = oi.Order.CreatedAt
                        })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Обновить товар (админ может редактировать любой товар)
        [HttpPut("items/{id}")]
        public async Task<IActionResult> UpdateItem(int id, [FromBody] AdminUpdateItemRequest request)
        {
            if (!IsAdmin())
            {
                return Unauthorized(new { message = "Требуются права администратора" });
            }

            try
            {
                var item = await _context.GameItems.FindAsync(id);
                if (item == null)
                {
                    return NotFound(new { message = "Товар не найден" });
                }

                // Админ может менять все поля
                if (request.Name != null) item.Name = request.Name;
                if (request.Description != null) item.Description = request.Description;
                if (request.ItemCode != null) item.ItemCode = request.ItemCode;
                if (request.Price.HasValue) item.Price = request.Price.Value;
                if (request.Quantity.HasValue) item.Quantity = request.Quantity.Value;
                if (request.ImageUrl != null) item.ImageUrl = request.ImageUrl;
                if (request.Rarity != null) item.Rarity = request.Rarity;
                if (request.Category != null) item.Category = request.Category;
                if (request.UserId.HasValue) item.UserId = request.UserId.Value;
                if (request.IsUserItem.HasValue) item.IsUserItem = request.IsUserItem.Value;

                item.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Товар успешно обновлен",
                    item = new
                    {
                        id = item.Id,
                        name = item.Name,
                        itemCode = item.ItemCode,
                        price = item.Price,
                        quantity = item.Quantity,
                        userId = item.UserId
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Удалить товар (админ может удалить даже если товар в заказах)
        [HttpDelete("items/{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            if (!IsAdmin())
            {
                return Unauthorized(new { message = "Требуются права администратора" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var item = await _context.GameItems
                    .Include(g => g.CartItems)
                    .Include(g => g.OrderItems)
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (item == null)
                {
                    return NotFound(new { message = "Товар не найден" });
                }

                // Удаляем товар из всех корзин
                _context.CartItems.RemoveRange(item.CartItems);

                // Удаляем сам товар
                _context.GameItems.Remove(item);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "Товар полностью удален (включая из корзин пользователей)" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Создать системный товар
        [HttpPost("items")]
        public async Task<IActionResult> CreateSystemItem([FromBody] CreateSystemItemRequest request)
        {
            if (!IsAdmin())
            {
                return Unauthorized(new { message = "Требуются права администратора" });
            }

            try
            {
                // Проверка уникальности артикула
                if (await _context.GameItems.AnyAsync(g => g.ItemCode == request.ItemCode))
                {
                    return BadRequest(new { message = "Товар с таким артикулом уже существует" });
                }

                var item = new GameItem
                {
                    Name = request.Name,
                    Description = request.Description ?? "Системный товар",
                    ItemCode = request.ItemCode,
                    Price = request.Price,
                    Quantity = request.Quantity,
                    ImageUrl = request.ImageUrl ?? "https://via.placeholder.com/300",
                    Rarity = request.Rarity ?? "Common",
                    Category = request.Category ?? "Other",
                    UserId = null, // Системный товар
                    IsUserItem = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.GameItems.Add(item);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Системный товар успешно создан",
                    item = new
                    {
                        id = item.Id,
                        name = item.Name,
                        itemCode = item.ItemCode,
                        price = item.Price,
                        quantity = item.Quantity
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // ========== СТАТИСТИКА ==========

        [HttpGet("stats")]
        public async Task<IActionResult> GetAdminStats()
        {
            if (!IsAdmin())
            {
                return Unauthorized(new { message = "Требуются права администратора" });
            }

            try
            {
                var totalUsers = await _context.Users.CountAsync();
                var totalItems = await _context.GameItems.CountAsync();
                var totalOrders = await _context.Orders.CountAsync();
                var totalRevenue = await _context.Orders.SumAsync(o => o.TotalPrice);
                var activeAdmins = await _context.Users.CountAsync(u => u.IsAdmin);

                var recentUsers = await _context.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Take(5)
                    .Select(u => new
                    {
                        id = u.Id,
                        username = u.Username,
                        email = u.Email,
                        createdAt = u.CreatedAt
                    })
                    .ToListAsync();

                var recentOrders = await _context.Orders
                    .Include(o => o.User)
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(5)
                    .Select(o => new
                    {
                        id = o.Id,
                        username = o.User.Username,
                        totalPrice = o.TotalPrice,
                        status = o.Status.ToString(),
                        createdAt = o.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new
                {
                    stats = new
                    {
                        totalUsers,
                        totalItems,
                        totalOrders,
                        totalRevenue,
                        activeAdmins,
                        averageOrderValue = totalOrders > 0 ? totalRevenue / totalOrders : 0
                    },
                    recentUsers,
                    recentOrders
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }

    // Модели запросов
    public class UpdateBalanceRequest
    {
        public decimal Amount { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string NewPassword { get; set; }
    }

    public class AdminUpdateItemRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ItemCode { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public string? ImageUrl { get; set; }
        public string? Rarity { get; set; }
        public string? Category { get; set; }
        public int? UserId { get; set; }
        public bool? IsUserItem { get; set; }
    }

    public class CreateSystemItemRequest
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public string ItemCode { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public string? ImageUrl { get; set; }
        public string? Rarity { get; set; }
        public string? Category { get; set; }
    }
}