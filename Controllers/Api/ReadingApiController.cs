using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EReaderApp.Data;
using EReaderApp.Models;
using System.Security.Claims;

namespace EReaderApp.Controllers.Api
{
    [Route("api/reading")]
    [ApiController]
    [Authorize]
    public class ReadingApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReadingApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/reading/state/5
        [HttpGet("state/{bookId}")]
        public async Task<IActionResult> GetReadingState(int bookId)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var readingState = await _context.ReadingStates
                    .FirstOrDefaultAsync(rs => rs.UserId == userId && rs.BookId == bookId);

                if (readingState == null)
                {
                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            bookId = bookId,
                            currentPage = 1,
                            zoomLevel = 1.0f,
                            viewMode = "double",
                            lastAccessed = (DateTime?)null
                        }
                    });
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        bookId = readingState.BookId,
                        currentPage = readingState.CurrentPage,
                        zoomLevel = readingState.ZoomLevel,
                        viewMode = readingState.ViewMode,
                        lastAccessed = readingState.LastAccessed
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // POST: api/reading/state
        [HttpPost("state")]
        public async Task<IActionResult> SaveReadingState([FromBody] SaveReadingStateRequest request)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var readingState = await _context.ReadingStates
                    .FirstOrDefaultAsync(rs => rs.UserId == userId && rs.BookId == request.BookId);

                if (readingState == null)
                {
                    readingState = new ReadingState
                    {
                        UserId = userId,
                        BookId = request.BookId,
                        CurrentPage = request.CurrentPage,
                        ZoomLevel = request.ZoomLevel,
                        ViewMode = request.ViewMode,
                        LastAccessed = DateTime.Now
                    };
                    _context.ReadingStates.Add(readingState);
                }
                else
                {
                    readingState.CurrentPage = request.CurrentPage;
                    readingState.ZoomLevel = request.ZoomLevel;
                    readingState.ViewMode = request.ViewMode;
                    readingState.LastAccessed = DateTime.Now;
                    _context.ReadingStates.Update(readingState);
                }

                // Update reading activity
                await UpdateReadingActivity(userId, request.BookId, request.CurrentPage, request.ReadingTimeMinutes);

                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Reading state saved successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // GET: api/reading/bookmarks/5
        [HttpGet("bookmarks/{bookId}")]
        public async Task<IActionResult> GetBookmarks(int bookId)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var bookmarks = await _context.BookMarks
                    .Where(b => b.UserId == userId && b.BookId == bookId)
                    .OrderBy(b => b.PageNumber)
                    .Select(b => new
                    {
                        id = b.Id,
                        title = b.Title,
                        pageNumber = b.PageNumber,
                        createdAt = b.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = bookmarks });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // POST: api/reading/bookmarks
        [HttpPost("bookmarks")]
        public async Task<IActionResult> CreateBookmark([FromBody] CreateBookmarkRequest request)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var bookmark = new Bookmark
                {
                    UserId = userId,
                    BookId = request.BookId,
                    PageNumber = request.PageNumber,
                    Title = request.Title,
                    CreatedAt = DateTime.Now
                };

                _context.BookMarks.Add(bookmark);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = bookmark.Id,
                        title = bookmark.Title,
                        pageNumber = bookmark.PageNumber,
                        createdAt = bookmark.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // DELETE: api/reading/bookmarks/5
        [HttpDelete("bookmarks/{id}")]
        public async Task<IActionResult> DeleteBookmark(int id)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var bookmark = await _context.BookMarks
                    .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

                if (bookmark == null)
                {
                    return NotFound(new { success = false, message = "Bookmark not found" });
                }

                _context.BookMarks.Remove(bookmark);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Bookmark deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // GET: api/reading/notes/5
        [HttpGet("notes/{bookId}")]
        public async Task<IActionResult> GetNotes(int bookId)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var notes = await _context.Notes
                    .Where(n => n.UserId == userId && n.BookId == bookId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new
                    {
                        id = n.Id,
                        content = n.Content,
                        createdAt = n.CreatedAt,
                        updatedAt = n.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = notes });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // POST: api/reading/notes
        [HttpPost("notes")]
        public async Task<IActionResult> CreateNote([FromBody] CreateNoteRequest request)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var note = new Note
                {
                    UserId = userId,
                    BookId = request.BookId,
                    Content = request.Content,
                    CreatedAt = DateTime.Now
                };

                _context.Notes.Add(note);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        id = note.Id,
                        content = note.Content,
                        createdAt = note.CreatedAt,
                        updatedAt = note.UpdatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        private async Task UpdateReadingActivity(int userId, int bookId, int currentPage, int readingTimeMinutes)
        {
            var readingActivity = await _context.ReadingActivities
                .FirstOrDefaultAsync(ra => ra.UserId == userId && ra.BookId == bookId);

            if (readingActivity == null)
            {
                readingActivity = new ReadingActivity
                {
                    UserId = userId,
                    BookId = bookId,
                    FirstAccess = DateTime.Now,
                    LastAccess = DateTime.Now,
                    AccessCount = 1,
                    LastPageRead = currentPage,
                    TotalPagesRead = 1,
                    TotalReadingTimeMinutes = readingTimeMinutes
                };
                _context.ReadingActivities.Add(readingActivity);
            }
            else
            {
                readingActivity.LastAccess = DateTime.Now;
                readingActivity.AccessCount++;

                if (readingTimeMinutes > 0)
                {
                    readingActivity.TotalReadingTimeMinutes += readingTimeMinutes;
                }

                if (currentPage != readingActivity.LastPageRead)
                {
                    readingActivity.TotalPagesRead++;
                    if (currentPage > readingActivity.LastPageRead)
                    {
                        readingActivity.LastPageRead = currentPage;
                    }
                }

                _context.ReadingActivities.Update(readingActivity);
            }
        }
    }

    // Controllers/Api/ReviewsApiController.cs
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ReviewsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReviewsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // POST: api/reviews
        [HttpPost]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewRequest request)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                // Check if user already reviewed this book
                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.FKIdBook == request.BookId && r.FKIdUser == userId);

                if (existingReview != null)
                {
                    // Update existing review
                    existingReview.Comment = request.Comment;
                    existingReview.Rating = request.Rating;
                    existingReview.CreatedAt = DateTime.Now;
                    _context.Update(existingReview);

                    await _context.SaveChangesAsync();
                    await UpdateBookScore(request.BookId);

                    return Ok(new
                    {
                        success = true,
                        message = "Review updated successfully",
                        data = new
                        {
                            id = existingReview.IdReview,
                            comment = existingReview.Comment,
                            rating = existingReview.Rating,
                            createdAt = existingReview.CreatedAt
                        }
                    });
                }
                else
                {
                    // Create new review
                    var review = new Review
                    {
                        FKIdBook = request.BookId,
                        FKIdUser = userId,
                        Comment = request.Comment,
                        Rating = request.Rating,
                        CreatedAt = DateTime.Now
                    };

                    _context.Add(review);
                    await _context.SaveChangesAsync();
                    await UpdateBookScore(request.BookId);

                    return Ok(new
                    {
                        success = true,
                        message = "Review created successfully",
                        data = new
                        {
                            id = review.IdReview,
                            comment = review.Comment,
                            rating = review.Rating,
                            createdAt = review.CreatedAt
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        // DELETE: api/reviews/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReview(int id)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var review = await _context.Reviews.FindAsync(id);

                if (review == null)
                {
                    return NotFound(new { success = false, message = "Review not found" });
                }

                if (review.FKIdUser != userId)
                {
                    return Forbid();
                }

                int bookId = review.FKIdBook;
                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();
                await UpdateBookScore(bookId);

                return Ok(new { success = true, message = "Review deleted successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred", error = ex.Message });
            }
        }

        private async Task UpdateBookScore(int bookId)
        {
            var book = await _context.Books.FindAsync(bookId);
            if (book != null)
            {
                var ratings = await _context.Reviews
                    .Where(r => r.FKIdBook == bookId)
                    .Select(r => r.Rating)
                    .ToListAsync();

                if (ratings.Any())
                {
                    book.Score = ratings.Average();
                    _context.Update(book);
                    await _context.SaveChangesAsync();
                }
            }
        }
    }

    // Request Models
    public class SaveReadingStateRequest
    {
        public int BookId { get; set; }
        public int CurrentPage { get; set; }
        public float ZoomLevel { get; set; }
        public string ViewMode { get; set; }
        public int ReadingTimeMinutes { get; set; }
    }

    public class CreateBookmarkRequest
    {
        public int BookId { get; set; }
        public int PageNumber { get; set; }
        public string Title { get; set; }
    }

    public class CreateNoteRequest
    {
        public int BookId { get; set; }
        public string Content { get; set; }
    }

    public class CreateReviewRequest
    {
        public int BookId { get; set; }
        public string Comment { get; set; }
        public int Rating { get; set; }
    }
}