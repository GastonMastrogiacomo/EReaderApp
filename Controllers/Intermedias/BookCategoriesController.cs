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
    public class BookCategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookCategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: BookCategories
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.BookCategories.Include(b => b.Book).Include(b => b.Category);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: BookCategories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.BookCategories == null)
            {
                return NotFound();
            }

            var bookCategory = await _context.BookCategories
                .Include(b => b.Book)
                .Include(b => b.Category)
                .FirstOrDefaultAsync(m => m.FKIdBook == id);
            if (bookCategory == null)
            {
                return NotFound();
            }

            return View(bookCategory);
        }

        // GET: BookCategories/Create
        public IActionResult Create()
        {
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author");
            ViewData["FKIdCategory"] = new SelectList(_context.Categories, "IdCategory", "CategoryName");
            return View();
        }

        // POST: BookCategories/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FKIdCategory,FKIdBook")] BookCategory bookCategory)
        {
            if (ModelState.IsValid)
            {
                _context.Add(bookCategory);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author", bookCategory.FKIdBook);
            ViewData["FKIdCategory"] = new SelectList(_context.Categories, "IdCategory", "CategoryName", bookCategory.FKIdCategory);
            return View(bookCategory);
        }

        // GET: BookCategories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.BookCategories == null)
            {
                return NotFound();
            }

            var bookCategory = await _context.BookCategories.FindAsync(id);
            if (bookCategory == null)
            {
                return NotFound();
            }
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author", bookCategory.FKIdBook);
            ViewData["FKIdCategory"] = new SelectList(_context.Categories, "IdCategory", "CategoryName", bookCategory.FKIdCategory);
            return View(bookCategory);
        }

        // POST: BookCategories/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("FKIdCategory,FKIdBook")] BookCategory bookCategory)
        {
            if (id != bookCategory.FKIdBook)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bookCategory);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookCategoryExists(bookCategory.FKIdBook))
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
            ViewData["FKIdBook"] = new SelectList(_context.Books, "IdBook", "Author", bookCategory.FKIdBook);
            ViewData["FKIdCategory"] = new SelectList(_context.Categories, "IdCategory", "CategoryName", bookCategory.FKIdCategory);
            return View(bookCategory);
        }

        // GET: BookCategories/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.BookCategories == null)
            {
                return NotFound();
            }

            var bookCategory = await _context.BookCategories
                .Include(b => b.Book)
                .Include(b => b.Category)
                .FirstOrDefaultAsync(m => m.FKIdBook == id);
            if (bookCategory == null)
            {
                return NotFound();
            }

            return View(bookCategory);
        }

        // POST: BookCategories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.BookCategories == null)
            {
                return Problem("Entity set 'ApplicationDbContext.BookCategories'  is null.");
            }
            var bookCategory = await _context.BookCategories.FindAsync(id);
            if (bookCategory != null)
            {
                _context.BookCategories.Remove(bookCategory);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BookCategoryExists(int id)
        {
            return (_context.BookCategories?.Any(e => e.FKIdBook == id)).GetValueOrDefault();
        }
    }
}
