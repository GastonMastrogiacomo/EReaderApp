using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EReaderApp.Data;
using EReaderApp.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace EReaderApp.Controllers.Api
{
    [Route("api/books")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BooksApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public BooksApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/books
        [HttpGet]
        [AllowAnonymous] // Allow anonymous access to browse books
        public async Task<IActionResult> GetBooks(
            [FromQuery] string? search = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] string? sortBy = "title",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                pageSize = Math.Min(pageSize, 50); // Limit page size

                IQueryable<Book> query = _context.Books;

                // Apply search filter
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(b =>
                        b.Title.Contains(search) ||
                        b.Author.Contains(search) ||
                        b.Description.Contains(search));
                }

                // Apply category filter
                if (categoryId.HasValue)
                {
                    query = query.Where(b =>
                        _context.BookCategories.Any(bc =>
                            bc.FKIdBook == b.IdBook && bc.FKIdCategory == categoryId.Value));
                }

                // Get total count before pagination
                var totalBooks = await query.CountAsync();

                // Apply sorting
                query = sortBy?.ToLower() switch
                {
                    "author" => query.OrderBy(b => b.Author).ThenBy(b => b.Title),
                    "rating" => query.OrderByDescending(b => b.Score ?? 0).ThenBy(b => b.Title),
                    "date" => query.OrderByDescending(b => b.IdBook),
                    _ => query.OrderBy(b => b.Title)
                };

                // Apply pagination
                var books = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(b => new
                    {
                        id = b.IdBook,
                        title = b.Title,
                        author = b.Author,
                        description = b.Description,
                        imageLink = b.ImageLink,
                        releaseDate = b.ReleaseDate,
                        pageCount = b.PageCount,
                        score = b.Score,
                        authorBio = b.AuthorBio
                    })
                    .ToListAsync();

                // Get review statistics for books
                var bookIds = books.Select(b => b.id).ToList();
                var reviewStats = await _context.Reviews
                    .Where(r => bookIds.Contains(r.FKIdBook))
                    .GroupBy(r => r.FKIdBook)
                    .Select(g => new
                    {
                        bookId = g.Key,
                        averageRating = g.Average(r => r.Rating),
                        reviewCount = g.Count()
                    })
                    .ToListAsync();

                var booksWithStats = books.Select(book =>
                {
                    var stats = reviewStats.FirstOrDefault(rs => rs.bookId == book.id);
                    return new
                    {
                        book.id,
                        book.title,
                        book.author,
                        book.description,
                        book.imageLink,
                        book.releaseDate,
                        book.pageCount,
                        book.score,
                        book.authorBio,
                        averageRating = stats?.averageRating ?? 0,
                        reviewCount = stats?.reviewCount ?? 0
                    };
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = booksWithStats,
                    pagination = new
                    {
                        currentPage = page,
                        pageSize = pageSize,
                        totalItems = totalBooks,
                        totalPages = (int)Math.Ceiling((double)totalBooks / pageSize),
                        hasNextPage = page < Math.Ceiling((double)totalBooks / pageSize),
                        hasPreviousPage = page > 1
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching books", error = ex.Message });
            }
        }

        // GET: api/books/5
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetBook(int id)
        {
            try
            {
                var book = await _context.Books
                    .FirstOrDefaultAsync(b => b.IdBook == id);

                if (book == null)
                {
                    return NotFound(new { success = false, message = "Book not found" });
                }

                // Get categories
                var categories = await _context.BookCategories
                    .Where(bc => bc.FKIdBook == id)
                    .Include(bc => bc.Category)
                    .Select(bc => new { id = bc.Category.IdCategory, name = bc.Category.CategoryName })
                    .ToListAsync();

                // Get reviews with user info
                var reviews = await _context.Reviews
                    .Where(r => r.FKIdBook == id)
                    .Include(r => r.User)
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new
                    {
                        id = r.IdReview,
                        comment = r.Comment,
                        rating = r.Rating,
                        createdAt = r.CreatedAt,
                        user = new
                        {
                            id = r.User.IdUser,
                            name = r.User.Name,
                            profilePicture = r.User.ProfilePicture
                        }
                    })
                    .ToListAsync();

                // Calculate review statistics
                var reviewStats = new
                {
                    totalReviews = reviews.Count,
                    averageRating = reviews.Any() ? reviews.Average(r => r.rating) : 0,
                    ratingDistribution = reviews
                        .GroupBy(r => r.rating)
                        .ToDictionary(g => g.Key, g => g.Count())
                };

                // Check if current user has reviewed this book
                object? userReview = null;
                if (User.Identity.IsAuthenticated)
                {
                    int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                    userReview = reviews.FirstOrDefault(r => r.user.id == userId);
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = book.IdBook,
                        title = book.Title,
                        author = book.Author,
                        description = book.Description,
                        imageLink = book.ImageLink,
                        releaseDate = book.ReleaseDate,
                        pageCount = book.PageCount,
                        score = book.Score,
                        authorBio = book.AuthorBio,
                        pdfPath = book.PdfPath,
                        categories = categories,
                        reviews = reviews,
                        reviewStats = reviewStats,
                        userReview = userReview
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching the book", error = ex.Message });
            }
        }

        // GET: api/books/categories
        [HttpGet("categories")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var categories = await _context.Categories
                    .Select(c => new
                    {
                        id = c.IdCategory,
                        name = c.CategoryName,
                        bookCount = _context.BookCategories.Count(bc => bc.FKIdCategory == c.IdCategory)
                    })
                    .Where(c => c.bookCount > 0)
                    .OrderBy(c => c.name)
                    .ToListAsync();

                return Ok(new { success = true, data = categories });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching categories", error = ex.Message });
            }
        }

        // GET: api/books/popular
        [HttpGet("popular")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPopularBooks([FromQuery] int limit = 10)
        {
            try
            {
                limit = Math.Min(limit, 50); // Limit to prevent abuse

                var popularBooks = await _context.Books
                    .Select(b => new
                    {
                        book = b,
                        averageRating = _context.Reviews
                            .Where(r => r.FKIdBook == b.IdBook)
                            .Select(r => (double?)r.Rating)
                            .Average(),
                        reviewCount = _context.Reviews.Count(r => r.FKIdBook == b.IdBook)
                    })
                    .Where(x => x.reviewCount >= 1)
                    .OrderByDescending(x => x.averageRating)
                    .ThenByDescending(x => x.reviewCount)
                    .Take(limit)
                    .Select(x => new
                    {
                        id = x.book.IdBook,
                        title = x.book.Title,
                        author = x.book.Author,
                        imageLink = x.book.ImageLink,
                        averageRating = x.averageRating ?? 0,
                        reviewCount = x.reviewCount
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = popularBooks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching popular books", error = ex.Message });
            }
        }

        // GET: api/books/recent
        [HttpGet("recent")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRecentBooks([FromQuery] int limit = 10)
        {
            try
            {
                limit = Math.Min(limit, 50);

                var recentBooks = await _context.Books
                    .OrderByDescending(b => b.IdBook)
                    .Take(limit)
                    .Select(b => new
                    {
                        id = b.IdBook,
                        title = b.Title,
                        author = b.Author,
                        imageLink = b.ImageLink,
                        releaseDate = b.ReleaseDate
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = recentBooks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching recent books", error = ex.Message });
            }
        }
    }
}