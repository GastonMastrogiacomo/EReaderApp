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

            // Create a UserProfileViewModel from the User
            var viewModel = new UserProfileViewModel
            {
                IdUser = user.IdUser,
                Name = user.Name,
                Email = user.Email,
                ProfilePicture = user.ProfilePicture,
                CreatedAt = user.CreatedAt
            };

            return View(viewModel);
        }

        // POST: Users/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "RequireAdminRole")]
        public async Task<IActionResult> Edit(int id, UserProfileViewModel model)
        {
            if (id != model.IdUser)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var user = await _context.Users.FindAsync(id);
                    if (user == null)
                    {
                        return NotFound();
                    }

                    // Update user properties
                    user.Name = model.Name;
                    user.Email = model.Email;
                    user.ProfilePicture = model.ProfilePicture;

                    // Only update password if a new one is provided
                    if (!string.IsNullOrEmpty(model.NewPassword))
                    {
                        user.Password = HashPassword(model.NewPassword);
                    }

                    _context.Update(user);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "User updated successfully";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(model.IdUser))
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
            return View(model);
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
                return Problem("Entity set 'ApplicationDbContext.Users' is null.");
            }

            var user = await _context.Users.FindAsync(id);
            if (user != null)
            {
                try
                {
                    // 1. Find and remove PublicationLikes by this user
                    var publicationLikes = await _context.PublicationLikes.Where(pl => pl.FKIdUser == id).ToListAsync();
                    if (publicationLikes.Any())
                    {
                        _context.PublicationLikes.RemoveRange(publicationLikes);
                    }

                    // 2. Find and remove Comments by this user
                    var comments = await _context.Comments.Where(c => c.FKIdUser == id).ToListAsync();
                    if (comments.Any())
                    {
                        _context.Comments.RemoveRange(comments);
                    }

                    // 3. Find and remove ReviewLikes by this user
                    var reviewLikes = await _context.ReviewLikes.Where(rl => rl.FKIdUser == id).ToListAsync();
                    if (reviewLikes.Any())
                    {
                        _context.ReviewLikes.RemoveRange(reviewLikes);
                    }

                    // 4. Find and remove Reviews by this user
                    var reviews = await _context.Reviews.Where(r => r.FKIdUser == id).ToListAsync();
                    if (reviews.Any())
                    {
                        _context.Reviews.RemoveRange(reviews);
                    }

                    // 5. Find and handle Publications by this user
                    var publications = await _context.Publications.Where(p => p.FKIdUser == id).ToListAsync();
                    if (publications.Any())
                    {
                        foreach (var publication in publications)
                        {
                            // Delete all Comments on this publication
                            var publicationComments = await _context.Comments
                                .Where(c => c.FKIdPublication == publication.IdPublication)
                                .ToListAsync();
                            if (publicationComments.Any())
                            {
                                _context.Comments.RemoveRange(publicationComments);
                            }

                            // Delete all PublicationLikes on this publication
                            var publicationLikess = await _context.PublicationLikes
                                .Where(pl => pl.FKIdPublication == publication.IdPublication)
                                .ToListAsync();
                            if (publicationLikess.Any())
                            {
                                _context.PublicationLikes.RemoveRange(publicationLikess);
                            }
                        }

                        // Now we can safely delete the publications
                        _context.Publications.RemoveRange(publications);
                    }

                    // 6. Delete the user's libraries and library books
                    var libraries = await _context.Libraries.Where(l => l.FKIdUser == id).ToListAsync();
                    if (libraries.Any())
                    {
                        // First remove all library books associated with these libraries
                        var libraryIds = libraries.Select(l => l.IdLibrary).ToList();
                        var libraryBooks = await _context.LibraryBooks
                            .Where(lb => libraryIds.Contains(lb.FKIdLibrary))
                            .ToListAsync();

                        if (libraryBooks.Any())
                        {
                            _context.LibraryBooks.RemoveRange(libraryBooks);
                        }

                        // Then remove the libraries
                        _context.Libraries.RemoveRange(libraries);
                    }

                    // 7. Delete user settings
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

                    TempData["SuccessMessage"] = "User deleted successfully.";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"Error deleting user: {ex.Message}";
                    return RedirectToAction(nameof(Index));
                }
            }

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
                CreatedAt = user.CreatedAt
            };

            // Get reading statistics
            var readingActivities = await _context.ReadingActivities
                .Where(ra => ra.UserId == userId)
                .Include(ra => ra.Book)
                .ToListAsync();

            ViewBag.TotalBooksRead = readingActivities.Count;
            ViewBag.TotalPagesRead = readingActivities.Sum(ra => ra.TotalPagesRead);
            ViewBag.TotalReadingHours = Math.Round(readingActivities.Sum(ra => ra.TotalReadingTimeMinutes) / 60.0, 1);

            // Calculate reading frequency and streak
            if (readingActivities.Any())
            {
                var readingDates = readingActivities
                    .SelectMany(ra => new[] { ra.FirstAccess.Date, ra.LastAccess.Date })
                    .Distinct()
                    .OrderBy(d => d)
                    .ToList();

                ViewBag.ReadingDaysCount = readingDates.Count;

                // Calculate reading streak
                int maxStreak = 0;
                int currentStreak = 0;
                for (int i = 1; i < readingDates.Count; i++)
                {
                    if ((readingDates[i] - readingDates[i - 1]).Days == 1)
                    {
                        currentStreak++;
                        maxStreak = Math.Max(maxStreak, currentStreak);
                    }
                    else
                    {
                        currentStreak = 0;
                    }
                }

                // Adjust maxStreak to include the first day in a streak
                if (maxStreak > 0) maxStreak++;

                ViewBag.ReadingStreak = maxStreak;
            }
            else
            {
                ViewBag.ReadingDaysCount = 0;
                ViewBag.ReadingStreak = 0;
            }

            // Get total reviews count
            ViewBag.TotalReviews = await _context.Reviews
                .Where(r => r.FKIdUser == userId)
                .CountAsync();

            ViewBag.TotalLibraries = await _context.Libraries
              .Where(l => l.FKIdUser == userId)
              .CountAsync();

            ViewBag.UserLibraries = await _context.Libraries
                .Where(l => l.FKIdUser == userId)
                .Take(3)
                .ToListAsync();

            // Get recent books with accurate progress calculation
            var recentBooks = readingActivities
                .OrderByDescending(ra => ra.LastAccess)
                .Take(4)
                .Select(ra => new {
                    Title = ra.Book?.Title ?? "Unknown Title",
                    Author = ra.Book?.Author ?? "Unknown Author",
                    ImageLink = ra.Book?.ImageLink,
                    // Simple integer calculation 
                    ReadingProgress = CalculateProgressSafely(ra),
                    LastReadDate = ra.LastAccess.ToString("MMM dd, yyyy"),
                    TotalTimeMinutes = ra.TotalReadingTimeMinutes,
                    TotalSessions = ra.AccessCount
                })
                .ToList();
            ViewBag.RecentBooks = recentBooks;
            return View(viewModel);
        }

        private int CalculateProgressSafely(ReadingActivity activity)
        {
            try
            {
                // Check for nulls and zero values
                if (activity == null || activity.Book == null ||
                    !activity.Book.PageCount.HasValue || activity.Book.PageCount.Value <= 0)
                {
                    return 0;
                }

                // Use LastPageRead for progress calculation
                // If LastPageRead is 0, we should show 0% progress since nothing has been read
                int lastPage = activity.LastPageRead;
                int totalPages = activity.Book.PageCount.Value;

                // If no pages have been viewed (LastPageRead = 0), show 0% progress
                if (lastPage == 0)
                {
                    return 0;
                }

                // Calculate percentage - handle edge cases
                double percentage = (double)lastPage / totalPages * 100.0;

                // Return the rounded percentage, capped at 100%
                return (int)Math.Min(100, Math.Round(percentage));
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error calculating reading progress: {ex.Message}");
                return 0;
            }
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

                // Update the password only if a new one is provided
                user.Password = HashPassword(model.NewPassword);
            }
            // If no new password is provided, keep the existing one
            // No need to set it explicitly as we're not changing it

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