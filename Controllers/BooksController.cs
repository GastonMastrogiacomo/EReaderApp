﻿using System;
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

namespace EReaderApp.Controllers
{

 
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BooksController(ApplicationDbContext context)
        {
            _context = context;
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
            return View();
        }

        [HttpPost]
        [Authorize(Policy = "RequireAdminRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Book book, IFormFile? file)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return Content("ModelState is invalid: " + string.Join("; ", errors));
            }

            // Handle file upload
            if (file != null && file.Length > 0)
            {
                // Crear la ruta completa del directorio uploads/books-PDF
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "books-PDF");

                // Asegurar que el directorio existe
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Generar un nombre de archivo único para evitar colisiones
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Guardar el archivo
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Guardar la ruta relativa en la base de datos (la que será accesible desde el navegador)
                book.PdfPath = "/uploads/books-PDF/" + uniqueFileName;
            }

            _context.Add(book);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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
            return View(book);
        }

        // POST: Books/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Edit(int id, [Bind("IdBook,Title,Author,Description,ImageLink,Subtitle,Editorial,PageCount,Score,PdfPath")] Book book)
        {
            if (id != book.IdBook)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(book);
                    await _context.SaveChangesAsync();
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
                return RedirectToAction(nameof(Index));
            }
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

        // Updated Search action method for BooksController.cs
        public async Task<IActionResult> Search(string query, int? categoryId)
        {
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

            // Execute the query to get the books
            var booksList = await books.ToListAsync();

            // Get review counts and average ratings for each book
            var bookIds = booksList.Select(b => b.IdBook).ToList();
            var reviewStats = await _context.Reviews
                .Where(r => bookIds.Contains(r.FKIdBook))
                .GroupBy(r => r.FKIdBook)
                .Select(g => new {
                    BookId = g.Key,
                    Count = g.Count(),
                    AverageRating = g.Average(r => r.Rating)
                })
                .ToDictionaryAsync(
                    x => x.BookId,
                    x => new { Count = x.Count, AverageRating = x.AverageRating }
                );

            // Store the review stats in ViewBag
            ViewBag.ReviewStats = reviewStats;

            return View(booksList);
        }

        // GET: Books/ViewDetails/5
        public async Task<IActionResult> ViewDetails(int id)
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

            return View(book);
        }


        private bool BookExists(int id)
        {
          return (_context.Books?.Any(e => e.IdBook == id)).GetValueOrDefault();
        }
    }
}
