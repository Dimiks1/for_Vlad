using Microsoft.EntityFrameworkCore;
using AbbaAPP.Data;

var builder = WebApplication.CreateBuilder(args);

// ========== ДОБАВЛЕНИЕ СЕРВИСОВ ==========

// DbContext для Entity Framework с PostgreSQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgresConnection"),
        npgsqlOptions => npgsqlOptions.MigrationsAssembly("AbbaAPP")
    )
);

// Razor Pages
builder.Services.AddRazorPages();

// MVC Controllers
builder.Services.AddControllers();

// HttpContextAccessor для работы с сессией
builder.Services.AddHttpContextAccessor();

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// CORS (если нужно для API)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ========== MIDDLEWARE PIPELINE ==========

// Применить миграции при запуске
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Используй Session перед авторизацией
app.UseSession();

// CORS
app.UseCors("AllowAll");

// Authorization
app.UseAuthorization();

// Маршруты
app.MapRazorPages();
app.MapControllers();

app.Run();