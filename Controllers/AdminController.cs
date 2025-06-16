using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EReaderApp.Data;
using EReaderApp.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace EReaderApp.Controllers
{
    [Authorize(Policy = "RequireAdminRole")]
    public class AdminController : Controller  
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)  
        {
            _context = context;
        }

        // Dashboard
        public async Task<IActionResult> Index()
        {
            var stats = new AdminDashboardViewModel
            {
                TotalUsers = await _context.Users.CountAsync(),
                TotalBooks = await _context.Books.CountAsync(),
                TotalCategories = await _context.Categories.CountAsync(),
                TotalPublications = await _context.Publications.CountAsync(),
                RecentUsers = await _context.Users.OrderByDescending(u => u.CreatedAt).Take(5).ToListAsync(),
                RecentBooks = await _context.Books.OrderByDescending(b => b.IdBook).Take(5).ToListAsync()
            };

            return View(stats);
        }

        // Manage Users
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users.ToListAsync();
            return View(users);
        }

        // Edit User Role
        public async Task<IActionResult> EditUserRole(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUserRole(int id, string role)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Prevent removing the last admin
            if (user.Role == "Admin" && role != "Admin")
            {
                // Count how many admins exist
                var adminCount = await _context.Users
                    .Where(u => u.Role == "Admin")
                    .CountAsync();

                if (adminCount <= 1)
                {
                    TempData["ErrorMessage"] = "Cannot remove the last administrator account.";
                    return RedirectToAction(nameof(EditUserRole), new { id });
                }
            }

            user.Role = role;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"User role for {user.Name} updated successfully.";
            return RedirectToAction(nameof(Users));
        }

        // System Statistics
        public async Task<IActionResult> Statistics()
        {
            // Get some interesting statistics for the admin
            var monthlyStats = await _context.Users
                .GroupBy(u => new { u.CreatedAt.Year, u.CreatedAt.Month })
                .Select(g => new {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    UserCount = g.Count()
                })
                .OrderBy(x => x.Year)
                .ThenBy(x => x.Month)
                .Take(12)
                .ToListAsync();

            // Top categories by book count
            var topCategories = await _context.BookCategories
                .GroupBy(bc => bc.FKIdCategory)
                .Select(g => new {
                    CategoryId = g.Key,
                    BookCount = g.Count()
                })
                .OrderByDescending(x => x.BookCount)
                .Take(5)
                .ToListAsync();

            // Map category IDs to names
            var categoryIds = topCategories.Select(tc => tc.CategoryId).ToList();
            var categories = await _context.Categories
                .Where(c => categoryIds.Contains(c.IdCategory))
                .ToDictionaryAsync(c => c.IdCategory, c => c.CategoryName);

            // Collect view data
            ViewBag.MonthlyUserStats = monthlyStats;
            ViewBag.TopCategories = topCategories
                .Select(tc => new {
                    CategoryName = categories.ContainsKey(tc.CategoryId) ? categories[tc.CategoryId] : "Unknown",
                    tc.BookCount
                })
                .ToList();

            return View();
        }

        // System Settings
        public IActionResult Settings()
        {
            return View();
        }

        // Activity Logs
        public async Task<IActionResult> ActivityLogs()
        {
            // Get recent reading activities
            var recentReadingActivities = await _context.ReadingActivities
                .Include(ra => ra.User)
                .Include(ra => ra.Book)
                .OrderByDescending(ra => ra.LastAccess)
                .Take(50)
                .ToListAsync();

            return View(recentReadingActivities);
        }
    }
}