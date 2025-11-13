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
        [Authorize]
        public IActionResult Create()
        {
            // For Admin users, show the user dropdown
            if (User.IsInRole("Admin"))
            {
                ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email");
                return View(new Library()); // Create an empty model for the form
            }
            else
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var model = new Library { FKIdUser = userId };
                return View(model);
            }
        }

        // POST: Libraries/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Library library, int? bookId)
        {
            ModelState.Remove("User");

            if (ModelState.IsValid)
            {
                try
                {
                    if (!User.IsInRole("Admin"))
                    {
                        int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                        library.FKIdUser = userId;
                    }

                    bool libraryExists = await _context.Libraries
                        .AnyAsync(l => l.ListName.Trim().ToLower() == library.ListName.Trim().ToLower()
                                  && l.FKIdUser == library.FKIdUser);

                    if (libraryExists)
                    {
                        TempData["ErrorMessage"] = $"You already have a library named '{library.ListName}'. Please choose a different name.";

                        if (bookId.HasValue)
                        {
                            return RedirectToAction("ViewDetails", "Books", new { id = bookId.Value });
                        }

                        if (User.IsInRole("Admin"))
                        {
                            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", library.FKIdUser);
                        }
                        return View(library);
                    }

                    _context.Add(library);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Library created successfully";

                    if (bookId.HasValue)
                    {
                        var libraryBook = new LibraryBook
                        {
                            FKIdLibrary = library.IdLibrary,
                            FKIdBook = bookId.Value
                        };
                        _context.LibraryBooks.Add(libraryBook);
                        await _context.SaveChangesAsync();

                        return RedirectToAction("ViewDetails", "Books", new { id = bookId.Value });
                    }

                    if (User.IsInRole("Admin"))
                    {
                        return RedirectToAction(nameof(Index));
                    }
                    else
                    {
                        return RedirectToAction(nameof(MyLibraries));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating library: {ex.Message}");
                    ModelState.AddModelError("", $"Error creating library: {ex.Message}");
                }
            }
            else
            {
                foreach (var state in ModelState)
                {
                    foreach (var error in state.Value.Errors)
                    {
                        System.Diagnostics.Debug.WriteLine($"Validation error: {state.Key} - {error.ErrorMessage}");
                    }
                }
            }

            if (User.IsInRole("Admin"))
            {
                ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", library.FKIdUser);
            }
            return View(library);
        }

        [Authorize]
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

            // If not admin, ensure that the library belongs to the current user
            if (!User.IsInRole("Admin"))
            {
                int currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                if (library.FKIdUser != currentUserId)
                {
                    return Forbid();
                }
            }

            // Only admins need to select a user from a list.
            if (User.IsInRole("Admin"))
            {
                ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", library.FKIdUser);
            }
            return View(library);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, [Bind("IdLibrary,ListName,FKIdUser")] Library library)
        {
            if (id != library.IdLibrary)
            {
                TempData["ErrorMessage"] = "ID mismatch. Please try again.";
                return RedirectToAction(nameof(MyLibraries));
            }

            var existingLibrary = await _context.Libraries
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.IdLibrary == id);

            if (existingLibrary == null)
            {
                TempData["ErrorMessage"] = "Library not found. It may have been deleted.";
                return RedirectToAction(nameof(MyLibraries));
            }

            if (!User.IsInRole("Admin"))
            {
                int currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                if (existingLibrary.FKIdUser != currentUserId)
                {
                    TempData["ErrorMessage"] = "You are not authorized to edit this library.";
                    return RedirectToAction(nameof(MyLibraries));
                }
                library.FKIdUser = currentUserId;
            }

            if (string.IsNullOrWhiteSpace(library.ListName))
            {
                ModelState.AddModelError("ListName", "Library name is required.");
                if (User.IsInRole("Admin"))
                {
                    ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", library.FKIdUser);
                }
                return View(library);
            }

            bool duplicateExists = await _context.Libraries
                .AnyAsync(l => l.ListName.Trim().ToLower() == library.ListName.Trim().ToLower()
                          && l.FKIdUser == library.FKIdUser
                          && l.IdLibrary != id);

            if (duplicateExists)
            {
                TempData["ErrorMessage"] = $"You already have another library named '{library.ListName}'. Please choose a different name.";
                if (User.IsInRole("Admin"))
                {
                    ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", library.FKIdUser);
                }
                return View(library);
            }

            try
            {
                existingLibrary.ListName = library.ListName;

                if (User.IsInRole("Admin"))
                {
                    existingLibrary.FKIdUser = library.FKIdUser;
                }

                _context.Update(existingLibrary);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Library renamed successfully.";

                if (User.IsInRole("Admin") && !IsAjaxRequest())
                {
                    ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", existingLibrary.FKIdUser);
                    return View(existingLibrary);
                }
                else
                {
                    return RedirectToAction(nameof(MyLibraries));
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!LibraryExists(library.IdLibrary))
                {
                    TempData["ErrorMessage"] = "Library no longer exists.";
                    return RedirectToAction(nameof(MyLibraries));
                }
                else
                {
                    Console.WriteLine($"Concurrency error: {ex.Message}");
                    TempData["ErrorMessage"] = "The library was modified by another user. Please try again.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating library: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while saving changes. Please try again.";
            }

            if (User.IsInRole("Admin"))
            {
                ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", library.FKIdUser);
            }
            return View(library);
        }

        // Helper method to check if the current request is an AJAX request
        private bool IsAjaxRequest()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> RenameLibrary(int id, string newName)
        {
            // Input validation
            if (string.IsNullOrWhiteSpace(newName))
            {
                TempData["ErrorMessage"] = "Library name is required.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            // Get the existing library
            var library = await _context.Libraries.FindAsync(id);
            if (library == null)
            {
                TempData["ErrorMessage"] = "Library not found.";
                return RedirectToAction(nameof(MyLibraries));
            }

            // Authorization check
            if (!User.IsInRole("Admin"))
            {
                int currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                if (library.FKIdUser != currentUserId)
                {
                    TempData["ErrorMessage"] = "You are not authorized to edit this library.";
                    return RedirectToAction(nameof(MyLibraries));
                }
            }

            try
            {
                // Simply update the name
                library.ListName = newName;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Library renamed successfully.";

                // Always redirect to MyLibraries, regardless of user role
                return RedirectToAction(nameof(MyLibraries));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error renaming library: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while renaming the library.";
                return RedirectToAction(nameof(Edit), new { id });
            }
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

            //return View(library);
            return RedirectToAction(nameof(MyLibraries));
        }

        // POST: Libraries/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Libraries == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Libraries'  is null.");
            }

            var library = await _context.Libraries.FindAsync(id);
            if (library != null)
            {
                // Verificar autorización
                if (!User.IsInRole("Admin"))
                {
                    int currentUserId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                    if (library.FKIdUser != currentUserId)
                    {
                        TempData["ErrorMessage"] = "You are not authorized to delete this library.";
                        return RedirectToAction(nameof(MyLibraries));
                    }
                }

                _context.Libraries.Remove(library);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Library deleted successfully.";
            }

            return RedirectToAction(nameof(MyLibraries));
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

            var bookIds = books.Select(b => b.IdBook).ToList();
            var reviewStats = new Dictionary<int, EReaderApp.Controllers.BooksController.ReviewStatistics>();

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
                reviewStats[item.BookId] = new EReaderApp.Controllers.BooksController.ReviewStatistics
                {
                    Count = item.Count,
                    AverageRating = (float)item.AverageRating
                };
            }

            ViewBag.ReviewStats = reviewStats;

            ViewBag.Library = library;
            return View(books);
        }

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

                // NUEVA VALIDACIÓN: Verificar si ya existe una biblioteca con ese nombre
                bool libraryExists = await _context.Libraries
                    .AnyAsync(l => l.ListName.Trim().ToLower() == model.ListName.Trim().ToLower()
                              && l.FKIdUser == userId);

                if (libraryExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"You already have a library named '{model.ListName}'. Please choose a different name."
                    });
                }

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

                bool libraryExists = await _context.Libraries
                    .AnyAsync(l => l.ListName.Trim().ToLower() == model.LibraryName.Trim().ToLower()
                              && l.FKIdUser == userId);

                if (libraryExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"You already have a library named '{model.LibraryName}'. Please choose a different name."
                    });
                }

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