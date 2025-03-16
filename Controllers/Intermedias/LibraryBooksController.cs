using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EReaderApp.Data;
using EReaderApp.Models;

namespace EReaderApp.Controllers.Intermedias
{
    public class LibraryBooksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LibraryBooksController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: LibraryBooks
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.LibraryBooks.Include(l => l.Book).Include(l => l.Library);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: LibraryBooks/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.LibraryBooks == null)
            {
                return NotFound();
            }

            var libraryBook = await _context.LibraryBooks
                .Include(l => l.Book)
                .Include(l => l.Library)
                .FirstOrDefaultAsync(m => m.FKIdLibrary == id);
            if (libraryBook == null)
            {
                return NotFound();
            }

            return View(libraryBook);
        }

        // GET: LibraryBooks/Create
        public IActionResult Create()
        {
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author");
            ViewData["FKIdLibrary"] = new SelectList(_context.Libraries, "IdLibrary", "ListName");
            return View();
        }

        // POST: LibraryBooks/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FKIdLibrary,FKIdBook")] LibraryBook libraryBook)
        {
            if (ModelState.IsValid)
            {
                _context.Add(libraryBook);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author", libraryBook.FKIdBook);
            ViewData["FKIdLibrary"] = new SelectList(_context.Libraries, "IdLibrary", "ListName", libraryBook.FKIdLibrary);
            return View(libraryBook);
        }

        // GET: LibraryBooks/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.LibraryBooks == null)
            {
                return NotFound();
            }

            var libraryBook = await _context.LibraryBooks.FindAsync(id);
            if (libraryBook == null)
            {
                return NotFound();
            }
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author", libraryBook.FKIdBook);
            ViewData["FKIdLibrary"] = new SelectList(_context.Libraries, "IdLibrary", "ListName", libraryBook.FKIdLibrary);
            return View(libraryBook);
        }

        // POST: LibraryBooks/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("FKIdLibrary,FKIdBook")] LibraryBook libraryBook)
        {
            if (id != libraryBook.FKIdLibrary)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(libraryBook);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LibraryBookExists(libraryBook.FKIdLibrary))
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
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author", libraryBook.FKIdBook);
            ViewData["FKIdLibrary"] = new SelectList(_context.Libraries, "IdLibrary", "ListName", libraryBook.FKIdLibrary);
            return View(libraryBook);
        }

        // GET: LibraryBooks/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.LibraryBooks == null)
            {
                return NotFound();
            }

            var libraryBook = await _context.LibraryBooks
                .Include(l => l.Book)
                .Include(l => l.Library)
                .FirstOrDefaultAsync(m => m.FKIdLibrary == id);
            if (libraryBook == null)
            {
                return NotFound();
            }

            return View(libraryBook);
        }

        // POST: LibraryBooks/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.LibraryBooks == null)
            {
                return Problem("Entity set 'ApplicationDbContext.LibraryBooks'  is null.");
            }
            var libraryBook = await _context.LibraryBooks.FindAsync(id);
            if (libraryBook != null)
            {
                _context.LibraryBooks.Remove(libraryBook);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool LibraryBookExists(int id)
        {
            return (_context.LibraryBooks?.Any(e => e.FKIdLibrary == id)).GetValueOrDefault();
        }
    }
}
