using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EReaderApp.Data;
using EReaderApp.Models;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.IO;

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

            // Verificar si el archivo existe físicamente
            string webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            string filePath = book.PdfPath.TrimStart('/');
            string physicalPath = Path.Combine(webRootPath, filePath);

            if (!System.IO.File.Exists(physicalPath))
            {
                TempData["ErrorMessage"] = "El archivo PDF no se encuentra disponible.";
                return RedirectToAction("Details", "Books", new { id = id });
            }

            // Si el usuario está autenticado, cargar o crear estado de lectura
            if (User.Identity.IsAuthenticated)
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                await TrackReading(userId, id);

                // Get reading state - ensure it's initialized to page 1 for new users
                var readingState = await GetReadingState(userId, id);
                if (readingState == null)
                {
                    // Create default reading state for new users starting at page 1
                    readingState = new ReadingState
                    {
                        UserId = userId,
                        BookId = id,
                        CurrentPage = 1, // Start at page 1
                        ZoomLevel = 1.0f,
                        ViewMode = "double",
                        LastAccessed = DateTime.Now
                    };
                    _context.ReadingStates.Add(readingState);
                    await _context.SaveChangesAsync();
                }

                ViewBag.ReadingState = readingState;
                ViewBag.BookMarks = await GetBookMarks(userId, id);
            }

            return View(book);
        }



        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SaveReadingState(int bookId, int currentPage, float zoomLevel, string viewMode, int readingTimeMinutes = 0)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // First get the book to check its page count
            var book = await _context.Books.FindAsync(bookId);
            int totalPages = book?.PageCount ?? 0;

            var readingState = await _context.ReadingStates
                .FirstOrDefaultAsync(rs => rs.UserId == userId && rs.BookId == bookId);

            // Store previous page to track page transitions
            int previousPage = readingState?.CurrentPage ?? 0;

            if (readingState == null)
            {
                // Create new reading state
                readingState = new ReadingState
                {
                    UserId = userId,
                    BookId = bookId,
                    CurrentPage = currentPage,
                    ZoomLevel = zoomLevel,
                    ViewMode = viewMode,
                    LastAccessed = DateTime.Now
                };
                _context.ReadingStates.Add(readingState);
            }
            else
            {
                // Update existing state
                readingState.CurrentPage = currentPage;
                readingState.ZoomLevel = zoomLevel;
                readingState.ViewMode = viewMode;
                readingState.LastAccessed = DateTime.Now;
                _context.ReadingStates.Update(readingState);
            }

            // Update reading activity with time spent and current page
            var readingActivity = await _context.ReadingActivities
                .FirstOrDefaultAsync(ra => ra.UserId == userId && ra.BookId == bookId);

            if (readingActivity != null)
            {
                // Add reading time if provided
                if (readingTimeMinutes > 0)
                {
                    readingActivity.TotalReadingTimeMinutes += readingTimeMinutes;
                }

                // If user moved to a new page, count it
                if (currentPage != previousPage)
                {
                    readingActivity.TotalPagesRead += 1;
                }

                // Always update the LastPageRead to the highest page seen
                if (currentPage > readingActivity.LastPageRead)
                {
                    readingActivity.LastPageRead = currentPage;
                }

                _context.ReadingActivities.Update(readingActivity);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SaveBookMark(int bookId, int pageNumber, string title)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Verificar que el libro existe
            var book = await _context.Books.FindAsync(bookId);
            if (book == null)
                return NotFound("Libro no encontrado");

            // Crear marcador
            var bookmark = new Bookmark
            {
                UserId = userId,
                BookId = bookId,
                PageNumber = pageNumber,
                Title = title,
                CreatedAt = DateTime.Now
            };

            _context.BookMarks.Add(bookmark);
            await _context.SaveChangesAsync();

            return Json(new { success = true, bookmarkId = bookmark.Id });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetBookMarks(int bookId)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var bookmarks = await _context.BookMarks
                .Where(b => b.UserId == userId && b.BookId == bookId)
                .OrderBy(b => b.PageNumber)
                .Select(b => new {
                    id = b.Id,
                    title = b.Title,
                    pageNumber = b.PageNumber,
                    createdAt = b.CreatedAt
                })
                .ToListAsync();

            return Json(bookmarks);
        }

        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> DeleteBookMark(int id)
        {
            if (!User.Identity.IsAuthenticated)
                return Unauthorized();

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var bookmark = await _context.BookMarks
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (bookmark == null)
                return NotFound("Marcador no encontrado");

            _context.BookMarks.Remove(bookmark);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // Métodos privados de ayuda
        private async Task TrackReading(int userId, int bookId, int currentPage = 1)
        {
            var readingActivity = await _context.ReadingActivities
                .FirstOrDefaultAsync(ra => ra.UserId == userId && ra.BookId == bookId);

            if (readingActivity == null)
            {
                // First time reading this book - initialize with the current page
                readingActivity = new ReadingActivity
                {
                    UserId = userId,
                    BookId = bookId,
                    FirstAccess = DateTime.Now,
                    LastAccess = DateTime.Now,
                    AccessCount = 1,
                    LastPageRead = currentPage,
                    TotalPagesRead = 1, // Start with 1 page viewed
                    TotalReadingTimeMinutes = 0
                };
                _context.ReadingActivities.Add(readingActivity);
            }
            else
            {
                readingActivity.LastAccess = DateTime.Now;
                readingActivity.AccessCount++;

                // If last page read is different from current page, increment total pages
                if (currentPage != readingActivity.LastPageRead)
                {
                    // Only count unique page views
                    readingActivity.TotalPagesRead++;
                    readingActivity.LastPageRead = currentPage;
                }

                _context.ReadingActivities.Update(readingActivity);
            }

            await _context.SaveChangesAsync();
        }

        private async Task<ReadingState> GetReadingState(int userId, int bookId)
        {
            return await _context.ReadingStates
                .FirstOrDefaultAsync(rs => rs.UserId == userId && rs.BookId == bookId);
        }

        private async Task<List<Bookmark>> GetBookMarks(int userId, int bookId)
        {
            return await _context.BookMarks
                .Where(b => b.UserId == userId && b.BookId == bookId)
                .OrderBy(b => b.PageNumber)
                .ToListAsync();
        }
    }


}