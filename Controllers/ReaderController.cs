using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EReaderApp.Data;
using EReaderApp.Models;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace EReaderApp.Controllers
{
    public class ReaderController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReaderController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Read(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null || string.IsNullOrEmpty(book.PdfPath))
            {
                return NotFound();
            }

            // Optionally, track reading history if user is logged in
            if (User.Identity.IsAuthenticated)
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                // Track reading history logic here if needed
            }

            return View(book);
        }
    }
}