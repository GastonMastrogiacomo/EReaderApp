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
    public class PublicationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PublicationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Publications
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Publications.Include(p => p.User);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: Publications/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Publications == null)
            {
                return NotFound();
            }

            var publication = await _context.Publications
                .Include(p => p.User)
                .FirstOrDefaultAsync(m => m.IdPublication == id);
            if (publication == null)
            {
                return NotFound();
            }

            return View(publication);
        }
        /*
        // GET: Publications/Create
        public IActionResult Create()
        {
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email");
            return View();
        }
       
        // POST: Publications/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdPublication,Title,Content,PubImageUrl,FKIdUser")] Publication publication)
        {
            if (ModelState.IsValid)
            {
                _context.Add(publication);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", publication.FKIdUser);
            return View(publication);
        }
        */

        [Authorize]
        public async Task<IActionResult> Create()
        {
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Publication publication, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                publication.FKIdUser = userId;

                // Handle image upload if provided
                if (imageFile != null && imageFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "publications");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    // Generate unique filename
                    var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(imageFile.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    publication.PubImageUrl = $"/uploads/publications/{fileName}";
                }

                _context.Add(publication);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(publication);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> LikePublication(int id)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Check if the user has already liked the publication
            var existingLike = await _context.PublicationLikes
                .FirstOrDefaultAsync(pl => pl.FKIdUser == userId && pl.FKIdPublication == id);

            if (existingLike == null)
            {
                // Add a new like
                _context.PublicationLikes.Add(new PublicationLike
                {
                    FKIdUser = userId,
                    FKIdPublication = id
                });

                await _context.SaveChangesAsync();
            }
            else
            {
                // Remove the like (toggle behavior)
                _context.PublicationLikes.Remove(existingLike);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Publications/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Publications == null)
            {
                return NotFound();
            }

            var publication = await _context.Publications.FindAsync(id);
            if (publication == null)
            {
                return NotFound();
            }
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", publication.FKIdUser);
            return View(publication);
        }

        // POST: Publications/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdPublication,Title,Content,PubImageUrl,FKIdUser")] Publication publication)
        {
            if (id != publication.IdPublication)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(publication);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PublicationExists(publication.IdPublication))
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
            ViewData["FKIdUser"] = new SelectList(_context.Users, "IdUser", "Email", publication.FKIdUser);
            return View(publication);
        }

        // GET: Publications/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Publications == null)
            {
                return NotFound();
            }

            var publication = await _context.Publications
                .Include(p => p.User)
                .FirstOrDefaultAsync(m => m.IdPublication == id);
            if (publication == null)
            {
                return NotFound();
            }

            return View(publication);
        }

        // POST: Publications/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Publications == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Publications'  is null.");
            }
            var publication = await _context.Publications.FindAsync(id);
            if (publication != null)
            {
                _context.Publications.Remove(publication);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PublicationExists(int id)
        {
          return (_context.Publications?.Any(e => e.IdPublication == id)).GetValueOrDefault();
        }
    }
}
