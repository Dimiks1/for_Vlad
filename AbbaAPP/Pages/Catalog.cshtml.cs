using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AbbaAPP.Data;
using AbbaAPP.Models;

namespace AbbaAPP.Pages
{
    public class CatalogModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CatalogModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<GameItem>? GameItems { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                // Загружаем все товары, кроме скрытых
                GameItems = await _context.GameItems
                    .Include(g => g.User)
                    .Where(g => !g.Name.StartsWith("[СКРЫТ] ")) // Исключаем скрытые товары
                    .OrderByDescending(g => g.CreatedAt)
                    .ToListAsync();
            }
            catch
            {
                GameItems = new List<GameItem>();
            }
        }
    }
}