using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AbbaAPP.Data;
using AbbaAPP.Models;

namespace AbbaAPP.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<GameItem>? GameItems { get; set; }

        public async Task OnGetAsync()
        {
            try
            {
                GameItems = await _context.GameItems.ToListAsync();
            }
            catch
            {
                GameItems = new List<GameItem>();
            }
        }
    }
}
