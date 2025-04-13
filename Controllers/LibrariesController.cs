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
    public class LibrariesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LibrariesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Libraries
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Libraries.Include(l => l.User);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Libraries/Details/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Libraries == null)
            {
                return NotFound();
            }

            var library = await _context.Libraries
                .Include(l => l.User)
                .FirstOrDefaultAsync(m => m.IdLibrary == id);
            if (library == null)
            {
                return NotFound();
            }

            return View(library);
        }

        // GET: Libraries/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email");
            return View();
        }

        // POST: Libraries/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Library library, int? bookId)
        {
            if (ModelState.IsValid)
            {
                // If user is not admin, ensure they can only create libraries for themselves
                if (!User.IsInRole("Admin"))
                {
                    int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                    library.FKIdUser = userId;
                }

                _context.Add(library);
                await _context.SaveChangesAsync();

                // If a book ID was provided, add the book to the newly created library
                if (bookId.HasValue)
                {
                    _context.LibraryBooks.Add(new LibraryBook
                    {
                        FKIdLibrary = library.IdLibrary,
                        FKIdBook = bookId.Value
                    });
                    await _context.SaveChangesAsync();

                    // Return to the book details page
                    return RedirectToAction("ViewDetails", "Books", new { id = bookId.Value });
                }

                // Otherwise return to the libraries list (admin) or my libraries (user)
                if (User.IsInRole("Admin"))
                {
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    return RedirectToAction(nameof(MyLibraries));
                }
            }

            // If we got this far, something failed
            if (User.IsInRole("Admin"))
            {
                ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", library.FKIdUser);
                return View(library);
            }
            else
            {
                // For non-admin users, return to the previous page
                if (bookId.HasValue)
                {
                    return RedirectToAction("ViewDetails", "Books", new { id = bookId.Value });
                }
                else
                {
                    return RedirectToAction(nameof(MyLibraries));
                }
            }
        }

        // GET: Libraries/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Libraries == null)
            {
                return NotFound();
            }

            var library = await _context.Libraries.FindAsync(id);
            if (library == null)
            {
                return NotFound();
            }
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", library.FKIdUser);
            return View(library);
        }

        // POST: Libraries/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("IdLibrary,ListName,FKIdUser")] Library library)
        {
            if (id != library.IdLibrary)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(library);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LibraryExists(library.IdLibrary))
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
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", library.FKIdUser);
            return View(library);
        }

        // GET: Libraries/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Libraries == null)
            {
                return NotFound();
            }

            var library = await _context.Libraries
                .Include(l => l.User)
                .FirstOrDefaultAsync(m => m.IdLibrary == id);
            if (library == null)
            {
                return NotFound();
            }

            return View(library);
        }

        // POST: Libraries/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Libraries == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Libraries'  is null.");
            }
            var library = await _context.Libraries.FindAsync(id);
            if (library != null)
            {
                _context.Libraries.Remove(library);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LibraryExists(int id)
        {
            return (_context.Libraries?.Any(e => e.IdLibrary == id)).GetValueOrDefault();
        }

        [Authorize]
        public async Task<IActionResult> MyLibraries()
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var libraries = await _context.Libraries
                .Where(l => l.FKIdUser == userId)
                .ToListAsync();

            return View(libraries);
        }

        [Authorize]
        public async Task<IActionResult> LibraryDetails(int id)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var library = await _context.Libraries
                .FirstOrDefaultAsync(l => l.IdLibrary == id && l.FKIdUser == userId);

            if (library == null)
            {
                return NotFound();
            }

            var books = await _context.LibraryBooks
                .Where(lb => lb.FKIdLibrary == id)
                .Include(lb => lb.Book)
                .Select(lb => lb.Book)
                .ToListAsync();

            ViewBag.Library = library;
            return View(books);
        }

        // NEW AJAX API ENDPOINTS //

        /// <summary>
        /// AJAX endpoint to get all libraries for the current user
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserLibraries()
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var libraries = await _context.Libraries
                    .Where(l => l.FKIdUser == userId)
                    .Select(l => new {
                        idLibrary = l.IdLibrary,
                        listName = l.ListName,
                        bookCount = _context.LibraryBooks.Count(lb => lb.FKIdLibrary == l.IdLibrary)
                    })
                    .ToListAsync();

                return Json(libraries);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// AJAX endpoint to create a new library
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLibrary([FromBody] CreateLibraryModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.ListName))
                {
                    return BadRequest(new { success = false, message = "Library name is required" });
                }

                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                var library = new Library
                {
                    ListName = model.ListName,
                    FKIdUser = userId
                };

                _context.Libraries.Add(library);
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    libraryId = library.IdLibrary,
                    libraryName = library.ListName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// AJAX endpoint to add a book to an existing library
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToLibraryAjax([FromBody] AddToLibraryModel model)
        {
            try
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                // Verify user owns the library
                var library = await _context.Libraries
                    .FirstOrDefaultAsync(l => l.IdLibrary == model.LibraryId && l.FKIdUser == userId);

                if (library == null)
                {
                    return NotFound(new { success = false, message = "Library not found" });
                }

                // Check if book already in library
                var existingEntry = await _context.LibraryBooks
                    .FirstOrDefaultAsync(lb => lb.FKIdLibrary == model.LibraryId && lb.FKIdBook == model.BookId);

                if (existingEntry != null)
                {
                    return Ok(new { success = true, alreadyExists = true, message = "Book already in library" });
                }

                // Add book to library
                _context.LibraryBooks.Add(new LibraryBook
                {
                    FKIdLibrary = model.LibraryId,
                    FKIdBook = model.BookId
                });

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    message = "Book added to library successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// AJAX endpoint to create a new library and add a book in one operation
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAndAddBook([FromBody] CreateAndAddBookModel model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.LibraryName))
                {
                    return BadRequest(new { success = false, message = "Library name is required" });
                }

                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

                // Create the new library
                var library = new Library
                {
                    ListName = model.LibraryName,
                    FKIdUser = userId
                };

                _context.Libraries.Add(library);
                await _context.SaveChangesAsync();

                // Add the book to the new library
                _context.LibraryBooks.Add(new LibraryBook
                {
                    FKIdLibrary = library.IdLibrary,
                    FKIdBook = model.BookId
                });

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    libraryId = library.IdLibrary,
                    libraryName = library.ListName,
                    message = "Book added to new library successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> AddToLibrary(int bookId, int libraryId)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Verify user owns the library
            var library = await _context.Libraries
                .FirstOrDefaultAsync(l => l.IdLibrary == libraryId && l.FKIdUser == userId);

            if (library == null)
            {
                return NotFound();
            }

            // Check if book already in library
            var existingEntry = await _context.LibraryBooks
                .FirstOrDefaultAsync(lb => lb.FKIdLibrary == libraryId && lb.FKIdBook == bookId);

            if (existingEntry == null)
            {
                // Add to library
                _context.LibraryBooks.Add(new LibraryBook
                {
                    FKIdLibrary = libraryId,
                    FKIdBook = bookId
                });

                await _context.SaveChangesAsync();
            }

            // Check if this is an AJAX request
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true });
            }

            // Normal form submission - redirect
            return RedirectToAction("LibraryDetails", new { id = libraryId });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> RemoveFromLibrary(int bookId, int libraryId)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Verify user owns the library
            var library = await _context.Libraries
                .FirstOrDefaultAsync(l => l.IdLibrary == libraryId && l.FKIdUser == userId);

            if (library == null)
            {
                return NotFound();
            }

            var libraryBook = await _context.LibraryBooks
                .FirstOrDefaultAsync(lb => lb.FKIdLibrary == libraryId && lb.FKIdBook == bookId);

            if (libraryBook != null)
            {
                _context.LibraryBooks.Remove(libraryBook);
                await _context.SaveChangesAsync();
            }

            // Check if this is an AJAX request
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true });
            }

            // Normal form submission - redirect
            return RedirectToAction("LibraryDetails", new { id = libraryId });
        }
    }

    // Models for API requests
    public class CreateLibraryModel
    {
        public string ListName { get; set; }
    }

    public class AddToLibraryModel
    {
        public int BookId { get; set; }
        public int LibraryId { get; set; }
    }

    public class CreateAndAddBookModel
    {
        public string LibraryName { get; set; }
        public int BookId { get; set; }
    }
}