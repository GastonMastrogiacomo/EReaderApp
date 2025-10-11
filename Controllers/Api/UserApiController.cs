using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EReaderApp.Data;
using System.Security.Claims;
using EReaderApp.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EReaderApp.Controllers.Api
{
    [Route("api/user")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class UserApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UserApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/user/profile
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                // Get reading statistics
                var readingActivities = await _context.ReadingActivities
                    .Where(ra => ra.UserId == userId)
                    .Include(ra => ra.Book)
                    .ToListAsync();

                var stats = new
                {
                    totalBooksRead = readingActivities.Count,
                    totalPagesRead = readingActivities.Sum(ra => ra.TotalPagesRead),
                    totalReadingHours = Math.Round(readingActivities.Sum(ra => ra.TotalReadingTimeMinutes) / 60.0, 1),
                    totalReviews = await _context.Reviews.CountAsync(r => r.FKIdUser == userId),
                    totalLibraries = await _context.Libraries.CountAsync(l => l.FKIdUser == userId)
                };

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = user.IdUser,
                        name = user.Name,
                        email = user.Email,
                        profilePicture = user.ProfilePicture,
                        role = user.Role,
                        createdAt = user.CreatedAt,
                        statistics = stats
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // GET: api/user/reading-activity
        [HttpGet("reading-activity")]
        public async Task<IActionResult> GetReadingActivity()
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var activities = await _context.ReadingActivities
                    .Where(ra => ra.UserId == userId)
                    .Include(ra => ra.Book)
                    .OrderByDescending(ra => ra.LastAccess)
                    .Select(ra => new
                    {
                        bookId = ra.BookId,
                        book = new
                        {
                            id = ra.Book.IdBook,
                            title = ra.Book.Title,
                            author = ra.Book.Author,
                            imageLink = ra.Book.ImageLink,
                            pageCount = ra.Book.PageCount
                        },
                        firstAccess = ra.FirstAccess,
                        lastAccess = ra.LastAccess,
                        accessCount = ra.AccessCount,
                        totalPagesRead = ra.TotalPagesRead,
                        lastPageRead = ra.LastPageRead,
                        totalReadingTimeMinutes = ra.TotalReadingTimeMinutes,
                        readingProgress = ra.Book.PageCount > 0
                            ? Math.Min(100, Math.Round((double)ra.LastPageRead / ra.Book.PageCount.Value * 100, 1))
                            : 0
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = activities });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }
    }
}

// Controllers/Api/LibrariesApiController.cs
namespace EReaderApp.Controllers.Api
{
    [Route("api/libraries")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class LibrariesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LibrariesApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/libraries
        [HttpGet]
        public async Task<IActionResult> GetLibraries()
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var libraries = await _context.Libraries
                    .Where(l => l.FKIdUser == userId)
                    .Select(l => new
                    {
                        id = l.IdLibrary,
                        name = l.ListName,
                        bookCount = _context.LibraryBooks.Count(lb => lb.FKIdLibrary == l.IdLibrary),
                        books = _context.LibraryBooks
                            .Where(lb => lb.FKIdLibrary == l.IdLibrary)
                            .Include(lb => lb.Book)
                            .Select(lb => new
                            {
                                id = lb.Book.IdBook,
                                title = lb.Book.Title,
                                author = lb.Book.Author,
                                imageLink = lb.Book.ImageLink,
                                description = lb.Book.Description,
                                releaseDate = lb.Book.ReleaseDate,
                                pageCount = lb.Book.PageCount,
                                score = lb.Book.Score,
                                authorBio = lb.Book.AuthorBio,
                                averageRating = _context.Reviews
                                    .Where(r => r.FKIdBook == lb.Book.IdBook)
                                    .Select(r => (double?)r.Rating)
                                    .Average() ?? 0,
                                reviewCount = _context.Reviews.Count(r => r.FKIdBook == lb.Book.IdBook)
                            })
                            .ToList()
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = libraries });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // GET: api/libraries/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetLibrary(int id)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var library = await _context.Libraries
                    .FirstOrDefaultAsync(l => l.IdLibrary == id && l.FKIdUser == userId);

                if (library == null)
                {
                    return NotFound(new { success = false, message = "Library not found" });
                }

                var books = await _context.LibraryBooks
                    .Where(lb => lb.FKIdLibrary == id)
                    .Include(lb => lb.Book)
                    .Select(lb => new
                    {
                        id = lb.Book.IdBook,
                        title = lb.Book.Title,
                        author = lb.Book.Author,
                        imageLink = lb.Book.ImageLink,
                        description = lb.Book.Description,
                        releaseDate = lb.Book.ReleaseDate,
                        pageCount = lb.Book.PageCount,
                        score = lb.Book.Score,
                        authorBio = lb.Book.AuthorBio,
                        averageRating = _context.Reviews
                            .Where(r => r.FKIdBook == lb.Book.IdBook)
                            .Select(r => (double?)r.Rating)
                            .Average() ?? 0,
                        reviewCount = _context.Reviews.Count(r => r.FKIdBook == lb.Book.IdBook)
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = library.IdLibrary,
                        name = library.ListName,
                        bookCount = books.Count,
                        books = books
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // POST: api/libraries
        [HttpPost]
        public async Task<IActionResult> CreateLibrary([FromBody] CreateLibraryRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Name))
                {
                    return BadRequest(new { success = false, message = "Library name is required" });
                }

                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var library = new Library
                {
                    ListName = request.Name,
                    FKIdUser = userId
                };

                _context.Libraries.Add(library);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = library.IdLibrary,
                        name = library.ListName,
                        bookCount = 0,
                        books = new List<object>()
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // POST: api/libraries/5/books/10
        [HttpPost("{libraryId}/books/{bookId}")]
        public async Task<IActionResult> AddBookToLibrary(int libraryId, int bookId)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var library = await _context.Libraries
                    .FirstOrDefaultAsync(l => l.IdLibrary == libraryId && l.FKIdUser == userId);

                if (library == null)
                {
                    return NotFound(new { success = false, message = "Library not found" });
                }

                var bookExists = await _context.Books.AnyAsync(b => b.IdBook == bookId);
                if (!bookExists)
                {
                    return NotFound(new { success = false, message = "Book not found" });
                }

                var existingEntry = await _context.LibraryBooks
                    .FirstOrDefaultAsync(lb => lb.FKIdLibrary == libraryId && lb.FKIdBook == bookId);

                if (existingEntry != null)
                {
                    return Ok(new { success = true, message = "Book already in library", alreadyExists = true });
                }

                _context.LibraryBooks.Add(new LibraryBook
                {
                    FKIdLibrary = libraryId,
                    FKIdBook = bookId
                });

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Book added to library successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // DELETE: api/libraries/5/books/10
        public async Task<IActionResult> RemoveBookFromLibrary(int libraryId, int bookId)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var library = await _context.Libraries
                    .FirstOrDefaultAsync(l => l.IdLibrary == libraryId && l.FKIdUser == userId);

                if (library == null)
                {
                    return NotFound(new { success = false, message = "Library not found" });
                }

                var libraryBook = await _context.LibraryBooks
                    .FirstOrDefaultAsync(lb => lb.FKIdLibrary == libraryId && lb.FKIdBook == bookId);

                if (libraryBook == null)
                {
                    return NotFound(new { success = false, message = "Book not found in library" });
                }

                _context.LibraryBooks.Remove(libraryBook);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Book removed from library successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }
    }

    public class CreateLibraryRequest
    {
        public string Name { get; set; }
    }
}