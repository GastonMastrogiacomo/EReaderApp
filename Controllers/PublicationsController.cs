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
using System.IO; // Added missing import
using Microsoft.AspNetCore.Http; // Added for IFormFile

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
        // Public access
        public async Task<IActionResult> Index()
        {
            var publications = await _context.Publications
                .Include(p => p.User)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Count likes and comments for each publication
            foreach (var publication in publications)
            {
                publication.Likes = await _context.PublicationLikes
                    .Where(pl => pl.FKIdPublication == publication.IdPublication)
                    .ToListAsync();

                // Check if current user has liked this publication
                if (User.Identity.IsAuthenticated)
                {
                    int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                    ViewData[$"UserLiked_{publication.IdPublication}"] =
                        publication.Likes.Any(l => l.FKIdUser == userId);
                }

                // Count comments
                ViewData[$"CommentsCount_{publication.IdPublication}"] =
                    await _context.Comments.CountAsync(c => c.FKIdPublication == publication.IdPublication);
            }

            return View(publications);
        }

        // GET: Publications/Details/5
        // Public access
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
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

            // Get comments for this publication
            var comments = await _context.Comments
                .Include(c => c.User)
                .Where(c => c.FKIdPublication == id)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Check if current user has liked this publication
            bool userHasLiked = false;
            if (User.Identity.IsAuthenticated)
            {
                int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                userHasLiked = await _context.PublicationLikes
                    .AnyAsync(pl => pl.FKIdPublication == id.Value && pl.FKIdUser == userId);
            }

            // Get like count
            int likeCount = await _context.PublicationLikes
                .CountAsync(pl => pl.FKIdPublication == id.Value);

            ViewBag.Comments = comments;
            ViewBag.UserHasLiked = userHasLiked;
            ViewBag.LikeCount = likeCount;

            return View(publication);
        }

        // GET: Publications/Create
        [Authorize]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Publications/Create
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Publication publication, IFormFile? imageFile)
        {
            // Set the user ID before model validation
            if (User.Identity.IsAuthenticated)
            {
                publication.FKIdUser = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                // Clear the model state error for FKIdUser since we're setting it manually
                ModelState.Remove("FKIdUser");
                ModelState.Remove("User"); // Clear User validation error too
            }

            // Now check if the model is valid
            if (ModelState.IsValid)
            {
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

                // Set creation timestamp
                publication.CreatedAt = DateTime.Now;

                _context.Add(publication);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Publication created successfully!";
                return RedirectToAction(nameof(Index));
            }

            // If we got this far, something failed - redisplay form
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
        [Authorize]
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

            // Check if user is the author or an admin
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            string userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (publication.FKIdUser != userId && userRole != "Admin")
            {
                return Forbid();
            }

            return View(publication);
        }

        // POST: Publications/Edit/5
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdPublication,Title,Content,PubImageUrl,FKIdUser,CreatedAt")] Publication publication)
        {
            if (id != publication.IdPublication)
            {
                return NotFound();
            }

            // Check if user is the author or an admin
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            string userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            var originalPublication = await _context.Publications.AsNoTracking().FirstOrDefaultAsync(p => p.IdPublication == id);
            if (originalPublication == null)
            {
                return NotFound();
            }

            if (originalPublication.FKIdUser != userId && userRole != "Admin")
            {
                return Forbid();
            }

            // Maintain the original user ID
            publication.FKIdUser = originalPublication.FKIdUser;

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
            return View(publication);
        }

        // GET: Publications/Delete/5
        [Authorize]
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

            // Check if user is the author or an admin
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            string userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (publication.FKIdUser != userId && userRole != "Admin")
            {
                return Forbid();
            }

            return View(publication);
        }

        // POST: Publications/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Publications == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Publications'  is null.");
            }

            var publication = await _context.Publications.FindAsync(id);
            if (publication == null)
            {
                return NotFound();
            }

            // Check if user is the author or an admin
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            string userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (publication.FKIdUser != userId && userRole != "Admin")
            {
                return Forbid();
            }

            _context.Publications.Remove(publication);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool PublicationExists(int id)
        {
            return (_context.Publications?.Any(e => e.IdPublication == id)).GetValueOrDefault();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddComment(int publicationId, string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                TempData["ErrorMessage"] = "Comment cannot be empty";
                return RedirectToAction(nameof(Details), new { id = publicationId });
            }

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var comment = new Comment
            {
                FKIdPublication = publicationId,
                FKIdUser = userId,
                Content = content,
                CreatedAt = DateTime.Now
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Comment added successfully";
            return RedirectToAction(nameof(Details), new { id = publicationId });
        }

        // POST: Publications/DeleteComment
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteComment(int commentId, int publicationId)
        {
            var comment = await _context.Comments.FindAsync(commentId);

            if (comment == null)
            {
                return NotFound();
            }

            // Check if user is authorized to delete (either comment author or admin)
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            string userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            if (comment.FKIdUser != userId && userRole != "Admin")
            {
                return Forbid();
            }

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Comment deleted successfully";
            return RedirectToAction(nameof(Details), new { id = publicationId });
        }
    }
}