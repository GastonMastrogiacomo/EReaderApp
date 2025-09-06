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

        [Authorize] // Add this attribute to require authentication
        public async Task<IActionResult> Read(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null || string.IsNullOrEmpty(book.PdfPath))
            {
                return NotFound();
            }

            // Check if PDF is available (either local file or Supabase URL)
            bool pdfAvailable = await IsPdfAvailable(book.PdfPath);

            if (!pdfAvailable)
            {
                TempData["ErrorMessage"] = "El archivo PDF no se encuentra disponible.";
                return RedirectToAction("ViewDetails", "Books", new { id = id });
            }

            // Get the authenticated user's ID
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Get or create reading state for authenticated user
            var userReadingState = await GetReadingState(userId, id);
            if (userReadingState == null)
            {
                // Create new reading state for authenticated user
                userReadingState = new ReadingState
                {
                    UserId = userId,
                    BookId = id,
                    CurrentPage = 1,
                    ZoomLevel = 1.0f,
                    ViewMode = "double",
                    LastAccessed = DateTime.Now
                };
                _context.ReadingStates.Add(userReadingState);
                await _context.SaveChangesAsync();
            }

            await TrackReading(userId, id, userReadingState.CurrentPage);
            ViewBag.BookMarks = await GetBookMarks(userId, id);
            ViewBag.ReadingState = userReadingState;

            return View(book);
        }

        private async Task<bool> IsPdfAvailable(string pdfPath)
        {
            try
            {
                if (pdfPath.StartsWith("https://") && pdfPath.Contains("supabase.co"))
                {
                    using var httpClient = new HttpClient();
                    var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, pdfPath));
                    return response.IsSuccessStatusCode;
                }
                else
                {
                    // For local files, check if file exists physically
                    string webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                    string filePath = pdfPath.TrimStart('/');
                    string physicalPath = Path.Combine(webRootPath, filePath);
                    return System.IO.File.Exists(physicalPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking PDF availability: {ex.Message}");
                return false;
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SaveReadingState(int bookId, int currentPage, float zoomLevel, string viewMode, int readingTimeMinutes = 0)
        {
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

                // Compare against both previousPage (from reading state) 
                // and LastPageRead (from activity) for better tracking
                bool pageChanged = (currentPage != previousPage) || (currentPage != readingActivity.LastPageRead);

                // If user moved to a new page, count it
                if (pageChanged && currentPage != readingActivity.LastPageRead)
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
            else
            {
                // Create a new reading activity
                readingActivity = new ReadingActivity
                {
                    UserId = userId,
                    BookId = bookId,
                    FirstAccess = DateTime.Now,
                    LastAccess = DateTime.Now,
                    AccessCount = 1,
                    LastPageRead = currentPage > 0 ? currentPage : 1,
                    TotalPagesRead = 1, // Start with 1 page viewed
                    TotalReadingTimeMinutes = readingTimeMinutes > 0 ? readingTimeMinutes : 0
                };
                _context.ReadingActivities.Add(readingActivity);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SaveBookMark(int bookId, int pageNumber, string title)
        {
            try
            {
                Console.WriteLine($"[BOOKMARK] Method called - BookId: {bookId}, Page: {pageNumber}, Title: '{title}'");

                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                Console.WriteLine($"[BOOKMARK] UserId: {userId}");

                // Check if bookmark already exists
                var existingBookmark = await _context.BookMarks
                    .FirstOrDefaultAsync(b => b.UserId == userId && b.BookId == bookId && b.PageNumber == pageNumber);

                if (existingBookmark != null)
                {
                    Console.WriteLine($"[BOOKMARK] Bookmark already exists with ID: {existingBookmark.Id}");
                    return Json(new { success = true, bookmarkId = existingBookmark.Id, message = "Bookmark already exists" });
                }

                var bookmark = new Bookmark
                {
                    UserId = userId,
                    BookId = bookId,
                    PageNumber = pageNumber,
                    Title = title,
                    CreatedAt = DateTime.UtcNow // Use UTC for PostgreSQL
                };

                _context.BookMarks.Add(bookmark);

                // Log the entity state
                var entry = _context.Entry(bookmark);
                Console.WriteLine($"[BOOKMARK] Entity State before save: {entry.State}");

                var result = await _context.SaveChangesAsync();

                Console.WriteLine($"[BOOKMARK] SaveChanges result: {result}");
                Console.WriteLine($"[BOOKMARK] Bookmark ID after save: {bookmark.Id}");
                Console.WriteLine($"[BOOKMARK] Entity State after save: {entry.State}");

                // Verify it was actually saved
                var savedBookmark = await _context.BookMarks
                    .FirstOrDefaultAsync(b => b.Id == bookmark.Id);

                if (savedBookmark == null)
                {
                    Console.WriteLine("[BOOKMARK ERROR] Bookmark not found after save!");
                    return Json(new { success = false, message = "Bookmark save verification failed" });
                }

                return Json(new { success = true, bookmarkId = bookmark.Id });
            }
            catch (DbUpdateException dbEx)
            {
                Console.WriteLine($"[BOOKMARK DB ERROR] {dbEx.Message}");
                Console.WriteLine($"[BOOKMARK DB ERROR] Inner: {dbEx.InnerException?.Message}");
                return Json(new { success = false, message = "Database error: " + dbEx.InnerException?.Message ?? dbEx.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BOOKMARK ERROR] {ex.Message}");
                Console.WriteLine($"[BOOKMARK ERROR] Stack: {ex.StackTrace}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetBookMarks(int bookId)
        {
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
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var bookmark = await _context.BookMarks
                .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

            if (bookmark == null)
                return NotFound("Marcador no encontrado");

            _context.BookMarks.Remove(bookmark);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        private async Task TrackReading(int userId, int bookId, int currentPage)
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

                // Track the page transition
                if (currentPage != readingActivity.LastPageRead)
                {
                    // Increment total pages read
                    readingActivity.TotalPagesRead++;

                    // Only update LastPageRead if the current page is higher
                    // This maintains a "high water mark" of furthest page reached
                    if (currentPage > readingActivity.LastPageRead)
                    {
                        readingActivity.LastPageRead = currentPage;
                    }
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