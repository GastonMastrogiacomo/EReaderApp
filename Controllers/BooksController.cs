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
        public async Task<IActionResult> Index()
        {
              return _context.Books != null ? 
                          View(await _context.Books.ToListAsync()) :
                          Problem("Entity set 'ApplicationDbContext.Books'  is null.");
        }

        // GET: Books/Details/5
        /*
        public async Task<IActionResult> Details(int? id)
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
        */

        // GET: Books/Create

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

        public IActionResult Create()
        {
            return View();
        }


        [HttpPost]
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
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, file.FileName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                book.PdfPath = "/uploads/" + file.FileName;
            }

            _context.Add(book);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }



        // GET: Books/Edit/5
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

        // Add to BooksController.cs
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

            return View(await books.ToListAsync());
        }

        private bool BookExists(int id)
        {
          return (_context.Books?.Any(e => e.IdBook == id)).GetValueOrDefault();
        }
    }
}
