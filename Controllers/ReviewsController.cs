using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EReaderApp.Data;
using EReaderApp.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace EReaderApp.Controllers
{
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Reviews
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Reviews.Include(r => r.Book).Include(r => r.User);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Reviews/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Reviews == null)
            {
                return NotFound();
            }

            var review = await _context.Reviews
                .Include(r => r.Book)
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.IdReview == id);
            if (review == null)
            {
                return NotFound();
            }

            return View(review);
        }

        // GET: Reviews/Create
        public IActionResult Create()
        {
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author");
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email");
            return View();
        }

        // POST: Reviews/Create
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Review review)
        {
            // If the user is submitting from the book details page, we need to handle it differently
            if (review.FKIdBook > 0 && review.Rating > 0 && !string.IsNullOrEmpty(review.Comment))
            {
                // Set the user ID from the current user
                review.FKIdUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                review.CreatedAt = DateTime.Now;

                // Check if the user has already reviewed this book
                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.FKIdBook == review.FKIdBook && r.FKIdUser == review.FKIdUser);

                if (existingReview != null)
                {
                    // User already has a review for this book, update it instead
                    existingReview.Comment = review.Comment;
                    existingReview.Rating = review.Rating;
                    existingReview.CreatedAt = DateTime.Now;
                    _context.Update(existingReview);
                    TempData["SuccessMessage"] = "¡Tu reseña ha sido actualizada!";
                }
                else
                {
                    // Add the new review
                    _context.Add(review);
                    TempData["SuccessMessage"] = "¡Tu reseña ha sido publicada!";
                }

                // Update book score with average rating
                await UpdateBookScore(review.FKIdBook);

                await _context.SaveChangesAsync();
                return RedirectToAction("ViewDetails", "Books", new { id = review.FKIdBook });
            }

            // For regular form submission through the admin panel
            if (ModelState.IsValid)
            {
                review.CreatedAt = DateTime.Now;
                _context.Add(review);
                await _context.SaveChangesAsync();

                // Update book score with average rating
                await UpdateBookScore(review.FKIdBook);

                return RedirectToAction(nameof(Index));
            }
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author", review.FKIdBook);
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", review.FKIdUser);
            return View(review);
        }

        // Helper method to update book score based on reviews
        private async Task UpdateBookScore(int bookId)
        {
            var book = await _context.Books.FindAsync(bookId);
            if (book != null)
            {
                // Calculate average rating
                var ratings = await _context.Reviews
                    .Where(r => r.FKIdBook == bookId)
                    .Select(r => r.Rating)
                    .ToListAsync();

                if (ratings.Any())
                {
                    book.Score = ratings.Average();
                    _context.Update(book);
                }
            }
        }

        // GET: Reviews/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Reviews == null)
            {
                return NotFound();
            }

            var review = await _context.Reviews.FindAsync(id);
            if (review == null)
            {
                return NotFound();
            }
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author", review.FKIdBook);
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", review.FKIdUser);
            return View(review);
        }

        // POST: Reviews/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdReview,Comment,Rating,FKIdBook,FKIdUser,CreatedAt")] Review review)
        {
            if (id != review.IdReview)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(review);
                    await _context.SaveChangesAsync();

                    // Update book score with average rating
                    await UpdateBookScore(review.FKIdBook);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReviewExists(review.IdReview))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author", review.FKIdBook);
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", review.FKIdUser);
            return View(review);
        }

        // GET: Reviews/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Reviews == null)
            {
                return NotFound();
            }

            var review = await _context.Reviews
                .Include(r => r.Book)
                .Include(r => r.User)
                .FirstOrDefaultAsync(m => m.IdReview == id);
            if (review == null)
            {
                return NotFound();
            }

            return View(review);
        }

        // POST: Reviews/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Reviews == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Reviews' is null.");
            }
            var review = await _context.Reviews.FindAsync(id);
            if (review != null)
            {
                int bookId = review.FKIdBook;
                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();

                // Update book score after deleting a review
                await UpdateBookScore(bookId);
            }

            return RedirectToAction(nameof(Index));
        }

        // Method to handle review submission from the book details page
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitReview(int bookId, string comment, int rating)
        {
            if (string.IsNullOrEmpty(comment) || rating < 1 || rating > 5)
            {
                TempData["ErrorMessage"] = "Por favor, ingresa un comentario y calificación válidos.";
                return RedirectToAction("ViewDetails", "Books", new { id = bookId });
            }

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Check if the user has already reviewed this book
            var existingReview = await _context.Reviews
                .FirstOrDefaultAsync(r => r.FKIdBook == bookId && r.FKIdUser == userId);

            if (existingReview != null)
            {
                // Update existing review
                existingReview.Comment = comment;
                existingReview.Rating = rating;
                existingReview.CreatedAt = DateTime.Now;
                _context.Update(existingReview);
                TempData["SuccessMessage"] = "¡Tu reseña ha sido actualizada!";
            }
            else
            {
                // Create new review
                var review = new Review
                {
                    FKIdBook = bookId,
                    FKIdUser = userId,
                    Comment = comment,
                    Rating = rating,
                    CreatedAt = DateTime.Now
                };
                _context.Add(review);
                TempData["SuccessMessage"] = "¡Tu reseña ha sido publicada!";
            }

            await _context.SaveChangesAsync();

            // Update book score with average rating
            await UpdateBookScore(bookId);

            return RedirectToAction("ViewDetails", "Books", new { id = bookId });
        }

        // Check if the current user has already reviewed a book
        [HttpGet]
        public async Task<IActionResult> UserReview(int bookId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { hasReview = false });
            }

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.FKIdBook == bookId && r.FKIdUser == userId);

            if (review == null)
            {
                return Json(new { hasReview = false });
            }

            return Json(new
            {
                hasReview = true,
                reviewId = review.IdReview,
                comment = review.Comment,
                rating = review.Rating
            });
        }

        private bool ReviewExists(int id)
        {
            return (_context.Reviews?.Any(e => e.IdReview == id)).GetValueOrDefault();
        }
    }
}