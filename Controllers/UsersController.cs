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
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Users
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Index()
        {
            return _context.Users != null ?
                        View(await _context.Users.ToListAsync()) :
                        Problem("Entity set 'ApplicationDbContext.Users'  is null.");
        }

        // GET: Users/Details/5
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Users == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.IdUser == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Users/Create
        [Authorize(Policy = "RequireAdminRole")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Create([Bind("IdUser,Name,Email,Password,ProfilePicture")] User user)
        {
            if (ModelState.IsValid)
            {
                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        // GET: Users/Edit/5
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Users == null)
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        // POST: Users/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Edit(int id, [Bind("IdUser,Name,Email,Password,ProfilePicture")] User user)
        {
            if (id != user.IdUser)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.IdUser))
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
            return View(user);
        }

        // GET: Users/Delete/5
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Users == null)
            {
                return NotFound();
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(m => m.IdUser == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Users == null)
            {
                return Problem("Entity set 'ApplicationDbContext.Users'  is null.");
            }

            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                try
                {
                    var readerSettings = await _context.ReaderSettings.Where(rs => rs.UserId == id).ToListAsync();
                    if (readerSettings.Any())
                    {
                        _context.ReaderSettings.RemoveRange(readerSettings);
                    }

                    var readingStates = await _context.ReadingStates.Where(rs => rs.UserId == id).ToListAsync();
                    if (readingStates.Any())
                    {
                        _context.ReadingStates.RemoveRange(readingStates);
                    }

                    var bookmarks = await _context.BookMarks.Where(b => b.UserId == id).ToListAsync();
                    if (bookmarks.Any())
                    {
                        _context.BookMarks.RemoveRange(bookmarks);
                    }

                    var notes = await _context.Notes.Where(n => n.UserId == id).ToListAsync();
                    if (notes.Any())
                    {
                        _context.Notes.RemoveRange(notes);
                    }

                    var readingActivities = await _context.ReadingActivities.Where(ra => ra.UserId == id).ToListAsync();
                    if (readingActivities.Any())
                    {
                        _context.ReadingActivities.RemoveRange(readingActivities);
                    }

                    // Finally remove the user
                    _context.Users.Remove(user);
                    await _context.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error deleting user: {ex.Message}";
                    return RedirectToAction(nameof(Index));
                }
            }

            TempData["SuccessMessage"] = "User deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return (_context.Users?.Any(e => e.IdUser == id)).GetValueOrDefault();
        }

        [Authorize]
        public async Task<IActionResult> Profile()
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            // Create a view model to avoid exposing the password
            var viewModel = new UserProfileViewModel
            {
                IdUser = user.IdUser,
                Name = user.Name,
                Email = user.Email,
                ProfilePicture = user.ProfilePicture,
                CreatedAt = user.CreatedAt // Map the CreatedAt property
            };

            return View(viewModel);
        }

        // Add this to the UsersController class
        [Authorize]
        public async Task<IActionResult> ProfileEdit()
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            var viewModel = new UserProfileViewModel
            {
                IdUser = user.IdUser,
                Name = user.Name,
                Email = user.Email,
                ProfilePicture = user.ProfilePicture
            };

            return View(viewModel);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("ProfileEdit", model);
            }

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            // Ensure user can only edit their own profile
            if (userId != model.IdUser)
            {
                return Forbid();
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Check if email is changed and if it's already in use by another user
            if (user.Email != model.Email &&
                await _context.Users.AnyAsync(u => u.Email == model.Email && u.IdUser != userId))
            {
                ModelState.AddModelError("Email", "Email is already in use");
                return View("ProfileEdit", model);
            }

            // Validate new password if provided
            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                if (!IsPasswordValid(model.NewPassword))
                {
                    ModelState.AddModelError("NewPassword", "Password must be at least 8 characters long and include uppercase letters, lowercase letters, numbers, and special characters.");
                    return View("ProfileEdit", model);
                }
            }

            // Handle profile picture upload
            if (model.ProfilePictureFile != null && model.ProfilePictureFile.Length > 0)
            {
                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(model.ProfilePictureFile.ContentType.ToLower()))
                {
                    ModelState.AddModelError("ProfilePictureFile", "Only image files (JPG, PNG, GIF) are allowed");
                    return View("ProfileEdit", model);
                }

                // Validate file size (max 5MB)
                if (model.ProfilePictureFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("ProfilePictureFile", "The file size should not exceed 5MB");
                    return View("ProfileEdit", model);
                }

                // Generate a unique filename
                string fileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ProfilePictureFile.FileName);

                // Set the path where the file will be saved
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profile-pictures");

                // Create directory if it doesn't exist
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string filePath = Path.Combine(uploadsFolder, fileName);

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProfilePictureFile.CopyToAsync(stream);
                }

                // Update the user's profile picture path
                user.ProfilePicture = "/uploads/profile-pictures/" + fileName;
            }

            // Update user information
            user.Name = model.Name;
            user.Email = model.Email;

            // If password is provided, hash it and update
            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                user.Password = HashPassword(model.NewPassword);
            }

            try
            {
                _context.Update(user);
                await _context.SaveChangesAsync();

                // Add success message
                TempData["SuccessMessage"] = "Your profile has been updated successfully!";

                return RedirectToAction("Profile");
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("", "An error occurred while saving your profile.");
                return View("ProfileEdit", model);
            }
        }

        // Password validation method
        private bool IsPasswordValid(string password)
        {
            // At least 8 characters
            if (password.Length < 8)
                return false;

            // Contains uppercase letter
            if (!password.Any(char.IsUpper))
                return false;

            // Contains lowercase letter
            if (!password.Any(char.IsLower))
                return false;

            // Contains number
            if (!password.Any(char.IsDigit))
                return false;

            // Contains special character
            if (!password.Any(c => !char.IsLetterOrDigit(c)))
                return false;

            return true;
        }


        // Password hashing method
        [HttpPost]
        // Same password hashing method as in AuthController
        private string HashPassword(string password)
        {
            return Convert.ToBase64String(
                System.Security.Cryptography.SHA256.Create()
                .ComputeHash(System.Text.Encoding.UTF8.GetBytes(password))
            );
        }

       
    }
}