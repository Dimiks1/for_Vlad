using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AbbaAPP.Data;
using AbbaAPP.Models;
using System.Text.Json;

namespace AbbaAPP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MyItemsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MyItemsController(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        // Получить все товары текущего пользователя
        [HttpGet]
        public async Task<IActionResult> GetMyItems()
        {
            try
            {
                var session = _httpContextAccessor.HttpContext?.Session;
                var userIdStr = session?.GetString("UserId");

                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован" });
                }

                var items = await _context.GameItems
                    .Where(g => g.UserId == userId)
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
                        isUserItem = g.IsUserItem
                    })
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Создать новый товар
        [HttpPost]
        public async Task<IActionResult> CreateItem([FromBody] CreateItemRequest request)
        {
            try
            {
                var session = _httpContextAccessor.HttpContext?.Session;
                var userIdStr = session?.GetString("UserId");

                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован" });
                }

                // Проверка обязательных полей
                if (string.IsNullOrWhiteSpace(request.Name) ||
                    string.IsNullOrWhiteSpace(request.ItemCode) ||
                    request.Price <= 0)
                {
                    return BadRequest(new { message = "Заполните обязательные поля: название, артикул и цена" });
                }

                // Проверка уникальности артикула
                if (await _context.GameItems.AnyAsync(g => g.ItemCode == request.ItemCode))
                {
                    return BadRequest(new { message = "Товар с таким артикулом уже существует" });
                }

                // Проверка количества
                if (request.Quantity < 0)
                {
                    return BadRequest(new { message = "Количество не может быть отрицательным" });
                }

                // Создание товара
                var gameItem = new GameItem
                {
                    Name = request.Name,
                    Description = request.Description ?? "Описание отсутствует",
                    ItemCode = request.ItemCode,
                    Price = request.Price,
                    Quantity = request.Quantity,
                    ImageUrl = request.ImageUrl ?? "https://via.placeholder.com/300",
                    Rarity = request.Rarity ?? "Common",
                    Category = request.Category ?? "Other",
                    UserId = userId,
                    IsUserItem = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.GameItems.Add(gameItem);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Товар успешно создан",
                    item = new
                    {
                        id = gameItem.Id,
                        name = gameItem.Name,
                        itemCode = gameItem.ItemCode,
                        price = gameItem.Price,
                        quantity = gameItem.Quantity
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Обновить существующий товар
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateItem(int id, [FromBody] UpdateItemRequest request)
        {
            try
            {
                var session = _httpContextAccessor.HttpContext?.Session;
                var userIdStr = session?.GetString("UserId");

                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован" });
                }

                // Найти товар и проверить владельца
                var gameItem = await _context.GameItems
                    .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

                if (gameItem == null)
                {
                    return NotFound(new { message = "Товар не найден или у вас нет прав на его редактирование" });
                }

                // Проверка уникальности артикула (если изменился)
                if (request.ItemCode != null && request.ItemCode != gameItem.ItemCode)
                {
                    if (await _context.GameItems.AnyAsync(g => g.ItemCode == request.ItemCode && g.Id != id))
                    {
                        return BadRequest(new { message = "Товар с таким артикулом уже существует" });
                    }
                }

                // Обновление полей
                if (request.Name != null) gameItem.Name = request.Name;
                if (request.Description != null) gameItem.Description = request.Description;
                if (request.ItemCode != null) gameItem.ItemCode = request.ItemCode;
                if (request.Price.HasValue) gameItem.Price = request.Price.Value;
                if (request.Quantity.HasValue) gameItem.Quantity = request.Quantity.Value;
                if (request.ImageUrl != null) gameItem.ImageUrl = request.ImageUrl;
                if (request.Rarity != null) gameItem.Rarity = request.Rarity;
                if (request.Category != null) gameItem.Category = request.Category;

                gameItem.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Товар успешно обновлен",
                    item = new
                    {
                        id = gameItem.Id,
                        name = gameItem.Name,
                        itemCode = gameItem.ItemCode,
                        price = gameItem.Price,
                        quantity = gameItem.Quantity
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Удалить товар
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(int id)
        {
            try
            {
                var session = _httpContextAccessor.HttpContext?.Session;
                var userIdStr = session?.GetString("UserId");

                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован" });
                }

                // Найти товар и проверить владельца
                var gameItem = await _context.GameItems
                    .FirstOrDefaultAsync(g => g.Id == id && g.UserId == userId);

                if (gameItem == null)
                {
                    return NotFound(new { message = "Товар не найден или у вас нет прав на его удаление" });
                }

                // Проверить, есть ли товар в заказах
                var inOrders = await _context.OrderItems.AnyAsync(o => o.GameItemId == id);

                if (inOrders)
                {
                    // Если товар был в заказах, не удаляем физически, а скрываем
                    // Обнуляем количество и помечаем как неактивный
                    gameItem.Quantity = 0;
                    gameItem.UpdatedAt = DateTime.UtcNow;

                    // Добавляем пометку в название
                    if (!gameItem.Name.StartsWith("[СКРЫТ] "))
                    {
                        gameItem.Name = $"[СКРЫТ] {gameItem.Name}";
                    }

                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        message = "Товар скрыт (был в заказах). " +
                                 "Физически удалить нельзя, так как он есть в истории заказов."
                    });
                }

                // Если товара нет в заказах, удаляем полностью
                // Каскадно удалится из всех корзин благодаря настройкам в ApplicationDbContext

                _context.GameItems.Remove(gameItem);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Товар успешно удален (включая удаление из всех корзин)" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        // Получить статистику по товарам пользователя
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            try
            {
                var session = _httpContextAccessor.HttpContext?.Session;
                var userIdStr = session?.GetString("UserId");

                if (!int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized(new { message = "Пользователь не авторизован" });
                }

                var items = await _context.GameItems
                    .Where(g => g.UserId == userId)
                    .ToListAsync();

                var totalItems = items.Count;
                var totalValue = items.Sum(g => g.Price * g.Quantity);
                var totalQuantity = items.Sum(g => g.Quantity);
                var outOfStock = items.Count(g => g.Quantity == 0);

                return Ok(new
                {
                    stats = new
                    {
                        totalItems,
                        totalValue,
                        totalQuantity,
                        outOfStock,
                        averagePrice = totalItems > 0 ? items.Average(g => g.Price) : 0
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }
    }

    public class CreateItemRequest
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public string ItemCode { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; } = 1;
        public string? ImageUrl { get; set; }
        public string? Rarity { get; set; }
        public string? Category { get; set; }
    }

    public class UpdateItemRequest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? ItemCode { get; set; }
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
        public string? ImageUrl { get; set; }
        public string? Rarity { get; set; }
        public string? Category { get; set; }
    }
}