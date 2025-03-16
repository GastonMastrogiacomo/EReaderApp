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
    public class PublicationLikesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PublicationLikesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: PublicationLikes
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.PublicationLikes.Include(p => p.Publication).Include(p => p.User);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: PublicationLikes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.PublicationLikes == null)
            {
                return NotFound();
            }

            var publicationLike = await _context.PublicationLikes
                .Include(p => p.Publication)
                .Include(p => p.User)
                .FirstOrDefaultAsync(m => m.FKIdUser == id);
            if (publicationLike == null)
            {
                return NotFound();
            }

            return View(publicationLike);
        }

        // GET: PublicationLikes/Create
        public IActionResult Create()
        {
            ViewData["FKIdPublication"] = new SelectList(_context.Publications, "IdPublication", "Content");
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email");
            return View();
        }

        // POST: PublicationLikes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FKIdUser,FKIdPublication")] PublicationLike publicationLike)
        {
            if (ModelState.IsValid)
            {
                _context.Add(publicationLike);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["FKIdPublication"] = new SelectList(_context.Publications, "IdPublication", "Content", publicationLike.FKIdPublication);
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", publicationLike.FKIdUser);
            return View(publicationLike);
        }

        // GET: PublicationLikes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.PublicationLikes == null)
            {
                return NotFound();
            }

            var publicationLike = await _context.PublicationLikes.FindAsync(id);
            if (publicationLike == null)
            {
                return NotFound();
            }
            ViewData["FKIdPublication"] = new SelectList(_context.Publications, "IdPublication", "Content", publicationLike.FKIdPublication);
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", publicationLike.FKIdUser);
            return View(publicationLike);
        }

        // POST: PublicationLikes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("FKIdUser,FKIdPublication")] PublicationLike publicationLike)
        {
            if (id != publicationLike.FKIdUser)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(publicationLike);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PublicationLikeExists(publicationLike.FKIdUser))
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
            ViewData["FKIdPublication"] = new SelectList(_context.Publications, "IdPublication", "Content", publicationLike.FKIdPublication);
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", publicationLike.FKIdUser);
            return View(publicationLike);
        }

        // GET: PublicationLikes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.PublicationLikes == null)
            {
                return NotFound();
            }

            var publicationLike = await _context.PublicationLikes
                .Include(p => p.Publication)
                .Include(p => p.User)
                .FirstOrDefaultAsync(m => m.FKIdUser == id);
            if (publicationLike == null)
            {
                return NotFound();
            }

            return View(publicationLike);
        }

        // POST: PublicationLikes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.PublicationLikes == null)
            {
                return Problem("Entity set 'ApplicationDbContext.PublicationLikes'  is null.");
            }
            var publicationLike = await _context.PublicationLikes.FindAsync(id);
            if (publicationLike != null)
            {
                _context.PublicationLikes.Remove(publicationLike);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PublicationLikeExists(int id)
        {
            return (_context.PublicationLikes?.Any(e => e.FKIdUser == id)).GetValueOrDefault();
        }
    }
}
