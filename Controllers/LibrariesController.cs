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
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Libraries.Include(l => l.User);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Libraries/Details/5
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
        public IActionResult Create()
        {
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email");
            return View();
        }

        // POST: Libraries/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdLibrary,ListName,FKIdUser")] Library library)
        {
            if (ModelState.IsValid)
            {
                _context.Add(library);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", library.FKIdUser);
            return View(library);
        }

        // GET: Libraries/Edit/5
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
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
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

            return RedirectToAction("LibraryDetails", new { id = libraryId });
        }


    }
}
