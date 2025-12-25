using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AbbaAPP.Data;
using AbbaAPP.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AbbaAPP.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AccountController(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Username) ||
                    string.IsNullOrWhiteSpace(request?.Email) ||
                    string.IsNullOrWhiteSpace(request?.Password))
                {
                    return BadRequest(new { message = "Все поля обязательны" });
                }

                if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                {
                    return BadRequest(new { message = "Пользователь с таким логином уже существует" });
                }

                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                {
                    return BadRequest(new { message = "Пользователь с таким email уже зарегистрирован" });
                }

                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email,
                    PasswordHash = HashPassword(request.Password),
                    Balance = 100, // Бонус при регистрации
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                SetUserSession(user);

                return Ok(new
                {
                    message = "Регистрация успешна",
                    user = new
                    {
                        id = user.Id,
                        username = user.Username,
                        email = user.Email,
                        isAdmin = user.IsAdmin,
                        balance = user.Balance,
                        createdAt = user.CreatedAt // Добавляем дату регистрации
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request?.Username) ||
                    string.IsNullOrWhiteSpace(request?.Password))
                {
                    return BadRequest(new { message = "Логин и пароль обязательны" });
                }

                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

                if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
                {
                    return Unauthorized(new { message = "Неверный логин или пароль" });
                }

                user.LastLogin = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                SetUserSession(user);

                return Ok(new
                {
                    message = "Успешный вход",
                    user = new
                    {
                        id = user.Id,
                        username = user.Username,
                        email = user.Email,
                        isAdmin = user.IsAdmin,
                        balance = user.Balance,
                        createdAt = user.CreatedAt // Добавляем дату регистрации
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            try
            {
                var session = _httpContextAccessor.HttpContext?.Session;
                var userIdStr = session?.GetString("UserId");

                if (string.IsNullOrWhiteSpace(userIdStr) || !int.TryParse(userIdStr, out int userId))
                {
                    return Unauthorized(new { message = "Не авторизован" });
                }

                // ВАЖНО: Получаем актуальные данные пользователя из БД
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    session?.Clear();
                    return Unauthorized(new { message = "Пользователь не найден" });
                }

                // Обновляем сессию актуальными данными
                var userData = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    isAdmin = user.IsAdmin,
                    balance = user.Balance,
                    createdAt = user.CreatedAt // Добавляем дату регистрации
                };

                session?.SetString("UserData", JsonSerializer.Serialize(userData));

                return Ok(new { user = userData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера: " + ex.Message });
            }
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            session?.Clear();
            return Ok(new { message = "Вы вышли из аккаунта" });
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private bool VerifyPassword(string password, string hash)
        {
            var hashOfInput = HashPassword(password);
            return hashOfInput.Equals(hash);
        }

        private void SetUserSession(User user)
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session != null)
            {
                session.SetString("UserId", user.Id.ToString());
                var userData = new
                {
                    id = user.Id,
                    username = user.Username,
                    email = user.Email,
                    isAdmin = user.IsAdmin,
                    balance = user.Balance,
                    createdAt = user.CreatedAt // Добавляем дату регистрации
                };
                session.SetString("UserData", JsonSerializer.Serialize(userData));
            }
        }
    }

    public class RegisterRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    public class LoginRequest
    {
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}