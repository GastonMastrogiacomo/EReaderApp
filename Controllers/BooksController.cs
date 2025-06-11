using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EReaderApp.Data;
using EReaderApp.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using EReaderApp.Services;

namespace EReaderApp.Controllers
{
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly StorageService _storageService;

        public BooksController(ApplicationDbContext context, StorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        // GET: Books
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Index()
        {
            return _context.Books != null ?
                        View(await _context.Books.ToListAsync()) :
                        Problem("Entity set 'ApplicationDbContext.Books'  is null.");
        }

        // GET: Books/Create
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Details(int id)
        {
            var book = await _context.Books
                .FirstOrDefaultAsync(m => m.IdBook == id);

            if (book == null)
            {
                return NotFound();
            }

            // Get categories for this book
            var categories = await _context.BookCategories
                .Where(bc => bc.FKIdBook == id)
                .Include(bc => bc.Category)
                .Select(bc => bc.Category)
                .ToListAsync();

            // Get reviews for this book
            var reviews = await _context.Reviews
                .Where(r => r.FKIdBook == id)
                .Include(r => r.User)
                .OrderByDescending(r => r.IdReview)
                .ToListAsync();

            // If user is authenticated, get their libraries for "Add to Library" functionality
            if (User.Identity.IsAuthenticated)
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var userLibraries = await _context.Libraries
                    .Where(l => l.FKIdUser == userId)
                    .ToListAsync();
                ViewBag.UserLibraries = userLibraries;
            }

            ViewBag.Categories = categories;
            ViewBag.Reviews = reviews;

            return View(book);
        }

        [Authorize(Policy = "RequireAdminRole")]
        public IActionResult Create()
        {
            ViewBag.Categories = new SelectList(_context.Categories, "IdCategory", "CategoryName");
            return View();    
        }

        [HttpPost]
        [Authorize(Policy = "RequireAdminRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Book book, IFormFile? file, IFormFile? coverImage, int categoryId)
        {
            if (categoryId <= 0)
            {
                ModelState.AddModelError("categoryId", "Please select a category for the book.");
                ViewBag.Categories = new SelectList(_context.Categories, "IdCategory", "CategoryName");
                return View(book);
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                ViewBag.Categories = new SelectList(_context.Categories, "IdCategory", "CategoryName");
                return View(book);
            }

            // Handle PDF file upload - SUPABASE FIRST APPROACH
            if (file != null && file.Length > 0)
            {
                try
                {
                    // Try Supabase upload first
                    book.PdfPath = await _storageService.UploadPdfAsync(file, file.FileName);

                    if (string.IsNullOrEmpty(book.PdfPath))
                    {
                        // Fallback to local storage
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "books-PDF");
                        if (!Directory.Exists(uploadsFolder))
                        {
                            Directory.CreateDirectory(uploadsFolder);
                        }

                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        book.PdfPath = "/uploads/books-PDF/" + uniqueFileName;
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("file", $"Error uploading file: {ex.Message}");
                    ViewBag.Categories = new SelectList(_context.Categories, "IdCategory", "CategoryName");
                    return View(book);
                }
            }
            else
            {
                ModelState.AddModelError("file", "Please upload a PDF file for the book.");
                ViewBag.Categories = new SelectList(_context.Categories, "IdCategory", "CategoryName");
                return View(book);
            }

            // Handle cover image upload - SUPABASE FIRST APPROACH
            if (coverImage != null && coverImage.Length > 0)
            {
                try
                {
                    book.ImageLink = await _storageService.UploadImageAsync(coverImage, "book-covers");

                    if (string.IsNullOrEmpty(book.ImageLink))
                    {
                        // Fallback to local storage
                        var coverUploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "book-covers");
                        if (!Directory.Exists(coverUploadsFolder))
                        {
                            Directory.CreateDirectory(coverUploadsFolder);
                        }

                        string coverFileName = Guid.NewGuid().ToString() + "_" + coverImage.FileName;
                        var coverFilePath = Path.Combine(coverUploadsFolder, coverFileName);

                        using (var stream = new FileStream(coverFilePath, FileMode.Create))
                        {
                            await coverImage.CopyToAsync(stream);
                        }

                        book.ImageLink = "/uploads/book-covers/" + coverFileName;
                    }
                }
                catch (Exception ex)
                {
                    // Log error but continue - cover image is optional
                    Console.WriteLine($"Error uploading cover image: {ex.Message}");
                }
            }

            // Ensure book has at least minimal required data
            if (string.IsNullOrWhiteSpace(book.Title) || string.IsNullOrWhiteSpace(book.Author))
            {
                ModelState.AddModelError("", "Book title and author are required.");
                ViewBag.Categories = new SelectList(_context.Categories, "IdCategory", "CategoryName");
                return View(book);
            }

            // Initialize any null values to avoid database issues
            book.Description = book.Description ?? string.Empty;
            book.ImageLink = book.ImageLink ?? string.Empty;
            book.ReleaseDate = book.ReleaseDate ?? string.Empty;
            book.AuthorBio = book.AuthorBio ?? string.Empty;

            // Ensure numeric fields have valid values
            if (book.PageCount <= 0)
            {
                book.PageCount = null;
            }

            if (book.Score <= 0)
            {
                book.Score = 0;
            }

            // Try to fetch book details from Open Library if not provided
            if (string.IsNullOrWhiteSpace(book.Description) || string.IsNullOrWhiteSpace(book.AuthorBio))
            {
                await TryFetchBookDetailsFromOpenLibrary(book);
            }

            // Try to fetch cover image from Google Books if no manual image/URL provided
            if (string.IsNullOrWhiteSpace(book.ImageLink))
            {
                await TryFetchCoverImageFromGoogleBooks(book);
            }

            // Add the book to the database
            _context.Add(book);
            await _context.SaveChangesAsync();

            // Create the book-category relationship
            var bookCategory = new BookCategory
            {
                FKIdBook = book.IdBook,
                FKIdCategory = categoryId
            };

            _context.BookCategories.Add(bookCategory);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Book was successfully uploaded!";
            return RedirectToAction(nameof(Index));
        }

        private async Task TryFetchBookDetailsFromOpenLibrary(Book book)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(book.Title)) return;

                using (var httpClient = new HttpClient())
                {
                    // Search for the book in Open Library
                    string searchQuery = Uri.EscapeDataString($"{book.Title} {book.Author}");
                    string searchUrl = $"https://openlibrary.org/search.json?q={searchQuery}&limit=5";

                    var response = await httpClient.GetAsync(searchUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();
                        using var document = JsonDocument.Parse(jsonString);

                        var root = document.RootElement;
                        if (root.TryGetProperty("docs", out var docs) && docs.GetArrayLength() > 0)
                        {
                            foreach (var doc in docs.EnumerateArray())
                            {
                                // Get first published date
                                if (string.IsNullOrEmpty(book.ReleaseDate) && doc.TryGetProperty("first_publish_year", out var firstYear))
                                {
                                    book.ReleaseDate = firstYear.GetInt32().ToString();
                                }

                                // Get work key for detailed information
                                if (doc.TryGetProperty("key", out var workKey))
                                {
                                    string workUrl = $"https://openlibrary.org{workKey.GetString()}.json";
                                    var workResponse = await httpClient.GetAsync(workUrl);

                                    if (workResponse.IsSuccessStatusCode)
                                    {
                                        var workJsonString = await workResponse.Content.ReadAsStringAsync();
                                        using var workDocument = JsonDocument.Parse(workJsonString);
                                        var workRoot = workDocument.RootElement;

                                        // Get description
                                        if (string.IsNullOrEmpty(book.Description) && workRoot.TryGetProperty("description", out var description))
                                        {
                                            if (description.ValueKind == JsonValueKind.String)
                                            {
                                                book.Description = description.GetString();
                                            }
                                            else if (description.ValueKind == JsonValueKind.Object && description.TryGetProperty("value", out var descValue))
                                            {
                                                book.Description = descValue.GetString();
                                            }
                                        }

                                        // Get author bio
                                        if (string.IsNullOrEmpty(book.AuthorBio) && workRoot.TryGetProperty("authors", out var authors) && authors.GetArrayLength() > 0)
                                        {
                                            var firstAuthor = authors[0];
                                            if (firstAuthor.TryGetProperty("author", out var authorRef) && authorRef.TryGetProperty("key", out var authorKey))
                                            {
                                                string authorUrl = $"https://openlibrary.org{authorKey.GetString()}.json";
                                                var authorResponse = await httpClient.GetAsync(authorUrl);

                                                if (authorResponse.IsSuccessStatusCode)
                                                {
                                                    var authorJsonString = await authorResponse.Content.ReadAsStringAsync();
                                                    using var authorDocument = JsonDocument.Parse(authorJsonString);
                                                    var authorRoot = authorDocument.RootElement;

                                                    if (authorRoot.TryGetProperty("bio", out var bio))
                                                    {
                                                        if (bio.ValueKind == JsonValueKind.String)
                                                        {
                                                            book.AuthorBio = bio.GetString();
                                                        }
                                                        else if (bio.ValueKind == JsonValueKind.Object && bio.TryGetProperty("value", out var bioValue))
                                                        {
                                                            book.AuthorBio = bioValue.GetString();
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                // Break after first successful match
                                if (!string.IsNullOrEmpty(book.Description) && !string.IsNullOrEmpty(book.AuthorBio) && !string.IsNullOrEmpty(book.ReleaseDate))
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching book details from Open Library: {ex.Message}");
            }
        }

        private async Task TryFetchCoverImageFromGoogleBooks(Book book)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(book.Title)) return;

                using (var httpClient = new HttpClient())
                {
                    string query = $"intitle:\"{Uri.EscapeDataString(book.Title)}\"";
                    if (!string.IsNullOrWhiteSpace(book.Author))
                    {
                        query += $"+inauthor:\"{Uri.EscapeDataString(book.Author)}\"";
                    }

                    string apiUrl = $"https://www.googleapis.com/books/v1/volumes?q={query}&maxResults=1";

                    var response = await httpClient.GetAsync(apiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();
                        using var document = JsonDocument.Parse(jsonString);

                        var root = document.RootElement;
                        if (root.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
                        {
                            var firstItem = items[0];
                            if (firstItem.TryGetProperty("volumeInfo", out var volumeInfo) &&
                                volumeInfo.TryGetProperty("imageLinks", out var imageLinks))
                            {
                                // Use the highest quality image available
                                book.ImageLink = imageLinks.TryGetProperty("extraLarge", out var extraLarge) ? extraLarge.GetString() :
                                                imageLinks.TryGetProperty("large", out var large) ? large.GetString() :
                                                imageLinks.TryGetProperty("medium", out var medium) ? medium.GetString() :
                                                imageLinks.TryGetProperty("small", out var small) ? small.GetString() :
                                                imageLinks.TryGetProperty("thumbnail", out var thumbnail) ? thumbnail.GetString() :
                                                imageLinks.TryGetProperty("smallThumbnail", out var smallThumbnail) ? smallThumbnail.GetString() : null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching cover image from Google Books: {ex.Message}");
            }
        }

        // GET: Books/Edit/5
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Books == null)
            {
                return NotFound();
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            // Get the current category for this book
            var bookCategory = await _context.BookCategories
                .FirstOrDefaultAsync(bc => bc.FKIdBook == id);

            int currentCategoryId = bookCategory?.FKIdCategory ?? 0;

            // Populate categories for dropdown
            ViewBag.Categories = new SelectList(_context.Categories, "IdCategory", "CategoryName");
            ViewBag.CurrentCategoryId = currentCategoryId;

            return View(book);
        }

        // POST: Books/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Edit(int id, [Bind("IdBook,Title,Author,Description,ImageLink,ReleaseDate,PageCount,Score,PdfPath,AuthorBio")] Book book, int categoryId)
        {
            if (id != book.IdBook)
            {
                return NotFound();
            }

            if (categoryId <= 0)
            {
                ModelState.AddModelError("categoryId", "Please select a category for the book.");
                ViewBag.Categories = new SelectList(_context.Categories, "IdCategory", "CategoryName");
                ViewBag.CurrentCategoryId = categoryId;
                return View(book);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(book);

                    // Find the existing book-category relationship
                    var existingBookCategory = await _context.BookCategories
                        .FirstOrDefaultAsync(bc => bc.FKIdBook == id);

                    if (existingBookCategory != null)
                    {
                        // If the category has changed
                        if (existingBookCategory.FKIdCategory != categoryId)
                        {
                            // Remove the old relationship
                            _context.BookCategories.Remove(existingBookCategory);
                            await _context.SaveChangesAsync();

                            // Create a new relationship
                            _context.BookCategories.Add(new BookCategory
                            {
                                FKIdBook = book.IdBook,
                                FKIdCategory = categoryId
                            });
                        }
                        // If category hasn't changed, no need to update it
                    }
                    else
                    {
                        // Create new relationship if one doesn't exist
                        _context.BookCategories.Add(new BookCategory
                        {
                            FKIdBook = book.IdBook,
                            FKIdCategory = categoryId
                        });
                    }

                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookExists(book.IdBook))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            // If we got this far, something failed - redisplay form
            ViewBag.Categories = new SelectList(_context.Categories, "IdCategory", "CategoryName", categoryId);
            ViewBag.CurrentCategoryId = categoryId;
            return View(book);
        }

        // GET: Books/Delete/5
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Books == null)
            {
                return NotFound();
            }

            var book = await _context.Books
                .FirstOrDefaultAsync(m => m.IdBook == id);
            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }

        // POST: Books/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Books == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Books'  is null.");
            }
            var book = await _context.Books.FindAsync(id);
            if (book != null)
            {
                _context.Books.Remove(book);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetBooks()
        {
            var books = await _context.Books.ToListAsync();
            return Ok(books);
        }

        public async Task<IActionResult> Search(string query, int? categoryId, string sortBy, int page = 1)
        {
            const int pageSize = 6; // 6 books per page

            IQueryable<Book> books = _context.Books;

            // Apply search query if provided
            if (!string.IsNullOrEmpty(query))
            {
                books = books.Where(b =>
                    b.Title.Contains(query) ||
                    b.Author.Contains(query) ||
                    b.Description.Contains(query));
            }

            // Filter by category if provided
            if (categoryId.HasValue)
            {
                books = books.Where(b =>
                    _context.BookCategories.Any(bc =>
                        bc.FKIdBook == b.IdBook && bc.FKIdCategory == categoryId.Value));
            }

            // Load all categories for the filter dropdown
            ViewBag.Categories = await _context.Categories.ToListAsync();
            ViewBag.CurrentCategory = categoryId;
            ViewBag.SearchQuery = query;
            ViewBag.SortBy = sortBy ?? "title";

            // Get total count before pagination
            var totalBooks = await books.CountAsync();

            // Apply sorting
            switch (sortBy?.ToLower())
            {
                case "author":
                    books = books.OrderBy(b => b.Author).ThenBy(b => b.Title);
                    break;
                case "rating":
                    // For rating sort, we need to join with reviews and calculate average
                    books = from book in books
                            let avgRating = _context.Reviews
                                .Where(r => r.FKIdBook == book.IdBook)
                                .Select(r => (double?)r.Rating)
                                .Average()
                            orderby avgRating descending, book.Title
                            select book;
                    break;
                case "title":
                default:
                    books = books.OrderBy(b => b.Title);
                    break;
            }

            // Apply pagination
            var paginatedBooks = await books
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Get review statistics for the current page of books
            var bookIds = paginatedBooks.Select(b => b.IdBook).ToList();
            var reviewStats = new Dictionary<int, ReviewStatistics>();

            var reviewData = await _context.Reviews
                .Where(r => bookIds.Contains(r.FKIdBook))
                .GroupBy(r => r.FKIdBook)
                .Select(g => new {
                    BookId = g.Key,
                    Count = g.Count(),
                    AverageRating = g.Average(r => r.Rating)
                })
                .ToListAsync();

            foreach (var item in reviewData)
            {
                reviewStats[item.BookId] = new ReviewStatistics
                {
                    Count = item.Count,
                    AverageRating = (float)item.AverageRating
                };
            }

            ViewBag.ReviewStats = reviewStats;

            // Pagination data
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalBooks / pageSize);
            ViewBag.TotalBooks = totalBooks;
            ViewBag.PageSize = pageSize;
            ViewBag.HasPreviousPage = page > 1;
            ViewBag.HasNextPage = page < ViewBag.TotalPages;

            return View(paginatedBooks);
        }

        // Clase auxiliar para mantener las estadísticas de reseñas de forma tipada
        public class ReviewStatistics
        {
            public int Count { get; set; }
            public float AverageRating { get; set; }
        }

        // GET: Books/ViewDetails/5
        public async Task<IActionResult>    ViewDetails(int id)
        {
            var book = await _context.Books
                .FirstOrDefaultAsync(m => m.IdBook == id);

            if (book == null)
            {
                return NotFound();
            }

            // Get categories for this book
            var categories = await _context.BookCategories
                .Where(bc => bc.FKIdBook == id)
                .Include(bc => bc.Category)
                .Select(bc => bc.Category)
                .ToListAsync();

            // Get reviews for this book with user info
            var reviews = await _context.Reviews
                .Where(r => r.FKIdBook == id)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // Calculate review statistics
            int totalReviews = reviews.Count;
            double averageRating = totalReviews > 0 ? reviews.Average(r => r.Rating) : 0;
            Dictionary<int, int> ratingDistribution = new Dictionary<int, int>();
            for (int i = 1; i <= 5; i++)
            {
                ratingDistribution[i] = reviews.Count(r => r.Rating == i);
            }

            // If user is authenticated, get their libraries for "Add to Library" functionality
            if (User.Identity.IsAuthenticated)
            {
                int userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                var userLibraries = await _context.Libraries
                    .Where(l => l.FKIdUser == userId)
                    .ToListAsync();
                ViewBag.UserLibraries = userLibraries;

                // Check if user has already reviewed this book
                var userReview = reviews.FirstOrDefault(r => r.FKIdUser == userId);
                ViewBag.UserReview = userReview;
            }

            ViewBag.Categories = categories;
            ViewBag.Reviews = reviews;
            ViewBag.TotalReviews = totalReviews;
            ViewBag.AverageRating = averageRating;
            ViewBag.RatingDistribution = ratingDistribution;

            var reviewIds = reviews.Select(r => r.IdReview).ToList();
            var reviewLikes = await _context.ReviewLikes
                .Where(rl => reviewIds.Contains(rl.FKIdReview))
                .ToListAsync();

            var reviewLikesInfo = new Dictionary<int, (int Count, bool UserHasLiked)>();
            foreach (var reviewId in reviewIds)
            {
                var likesForReview = reviewLikes.Where(rl => rl.FKIdReview == reviewId).ToList();
                bool userHasLiked = false;

                if (User.Identity.IsAuthenticated)
                {
                    int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                    userHasLiked = likesForReview.Any(rl => rl.FKIdUser == userId);
                }

                reviewLikesInfo[reviewId] = (likesForReview.Count, userHasLiked);
            }

            ViewBag.ReviewLikes = reviewLikesInfo;

            reviews = reviews.OrderByDescending(r => reviewLikesInfo.ContainsKey(r.IdReview) ? reviewLikesInfo[r.IdReview].Item1 : 0)
            .ToList();

            ViewBag.Reviews = reviews;


            return View(book);
        }

        private async Task TryFetchAuthorDetailsFromGoogleBooks(Book book)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(book.Author)) return;

                // Create a new HttpClient instance for this request
                using (var httpClient = new HttpClient())
                {
                    // Construct search query for author information
                    string authorQuery = $"inauthor:\"{Uri.EscapeDataString(book.Author)}\"";
                    string apiUrl = $"https://www.googleapis.com/books/v1/volumes?q={authorQuery}&maxResults=10";

                    // Make the request
                    var response = await httpClient.GetAsync(apiUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var jsonString = await response.Content.ReadAsStringAsync();
                        using var document = JsonDocument.Parse(jsonString);

                        var root = document.RootElement;
                        if (root.TryGetProperty("items", out var items))
                        {
                            foreach (var item in items.EnumerateArray())
                            {
                                // Check if this item has volumeInfo
                                if (item.TryGetProperty("volumeInfo", out var volumeInfo))
                                {
                                    // Some books have an explicit author info section that Google identifies
                                    if (volumeInfo.TryGetProperty("authors", out var authors) &&
                                        authors.GetArrayLength() > 0 &&
                                        volumeInfo.TryGetProperty("description", out var description))
                                    {
                                        string authorName = authors[0].GetString();
                                        if (authorName.Contains(book.Author, StringComparison.OrdinalIgnoreCase))
                                        {
                                            string desc = description.GetString();

                                            // Look for an "About the Author" section in the description
                                            int aboutIndex = desc.IndexOf("About the Author", StringComparison.OrdinalIgnoreCase);
                                            if (aboutIndex >= 0)
                                            {
                                                // Take the text after "About the Author"
                                                string bio = desc.Substring(aboutIndex);

                                                // Limit to a reasonable length
                                                if (bio.Length > 500)
                                                {
                                                    bio = bio.Substring(0, 500) + "...";
                                                }

                                                book.AuthorBio = bio;
                                                return; // Found a dedicated author section
                                            }
                                        }
                                    }
                                }
                            }

                            // If we reach here, we didn't find a dedicated author section
                            // Fall back to the original method of finding a sentence about the author
                            foreach (var item in items.EnumerateArray())
                            {
                                if (item.TryGetProperty("volumeInfo", out var volumeInfo) &&
                                    volumeInfo.TryGetProperty("description", out var description))
                                {
                                    string desc = description.GetString();
                                    if (!string.IsNullOrWhiteSpace(desc) && desc.Contains(book.Author))
                                    {
                                        // Extract a sentence about the author
                                        var sentences = desc.Split(new[] { '.', '!', '?' })
                                            .Where(s => s.Contains(book.Author));

                                        if (sentences.Any())
                                        {
                                            book.AuthorBio = sentences.First().Trim() + ".";
                                            return;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the error but continue - author info is not critical
                Console.WriteLine($"Error fetching author details: {ex.Message}");
            }
        }

        private bool BookExists(int id)
        {
            return (_context.Books?.Any(e => e.IdBook == id)).GetValueOrDefault();
        }
    }
}