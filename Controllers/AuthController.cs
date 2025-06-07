using Microsoft.AspNetCore.Mvc;
using EReaderApp.Data;
using EReaderApp.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using Microsoft.EntityFrameworkCore;

namespace EReaderApp.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Login(string returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, string returnUrl = null)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email);

            if (user == null || !VerifyPassword(password, user.Password))
            {
                ModelState.AddModelError("", "Invalid email or password");
                ViewBag.ReturnUrl = returnUrl;
                return View();
            }

            await SignInUserAsync(user, true);

            // If user is an Admin and no specific return URL is provided, redirect to Admin Dashboard
            if (user.Role == "Admin" && string.IsNullOrEmpty(returnUrl))
            {
                return RedirectToAction("Index", "Admin");
            }

            // Otherwise, redirect to the return URL or home page
            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction("Index", "Home");
        }

        // Google login challenge
        public IActionResult GoogleLogin(string returnUrl = null)
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleResponse", new { returnUrl = returnUrl })
            };

            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        // Google response handler
        public async Task<IActionResult> GoogleResponse(string returnUrl = null)
        {
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

            if (!result.Succeeded)
                return RedirectToAction("Login");

            // Get user info from Google
            var googleEmail = result.Principal.FindFirstValue(ClaimTypes.Email);
            var googleName = result.Principal.FindFirstValue(ClaimTypes.Name);

            // Find or create the user in our database
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == googleEmail);

            if (user == null)
            {
                // Create a new user
                user = new User
                {
                    Email = googleEmail,
                    Name = googleName,
                    Password = HashPassword(Guid.NewGuid().ToString()), // Random password since they'll use Google auth
                    CreatedAt = DateTime.Now,
                    Role = "User"
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            // Sign in the user
            await SignInUserAsync(user, true);

            // Redirect appropriately
            if (user.Role == "Admin" && string.IsNullOrEmpty(returnUrl))
            {
                return RedirectToAction("Index", "Admin");
            }

            return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? Redirect(returnUrl)
                : RedirectToAction("Index", "Home");
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(User model)
        {
            if (ModelState.IsValid)
            {
                // Check if user already exists
                if (_context.Users.Any(u => u.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email is already in use");
                    return View(model);
                }

                // Validate password strength
                if (!IsPasswordValid(model.Password))
                {
                    ModelState.AddModelError("Password", "Password must be at least 8 characters long and include uppercase letters, lowercase letters, numbers, and special characters.");
                    return View(model);
                }

                // Hash the password
                model.Password = HashPassword(model.Password);

                // Set the creation date
                model.CreatedAt = DateTime.Now;

                // Set default role to "User"
                model.Role = "User";

                _context.Users.Add(model);
                await _context.SaveChangesAsync();

                // Auto-login after registration
                await SignInUserAsync(model, true);

                return RedirectToAction("Index", "Home");
            }
            return View(model);
        }

        public async Task<IActionResult> Logout()
        {
            // Force deletion of the authentication cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Ensure cookies are cleared from browser
            Response.Cookies.Delete(".AspNetCore.Cookies");

            return RedirectToAction("Index", "Home");
        }

        // Add AccessDenied action to handle unauthorized access
        public IActionResult AccessDenied(string returnUrl = null)
        {
            TempData["ErrorMessage"] = "You don't have permission to access this resource.";
            return RedirectToAction("Index", "Home");
        }

        // Helper method to sign in users
        private async Task SignInUserAsync(User user, bool isPersistent)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.IdUser.ToString()),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = isPersistent,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }

        // Simple password hashing (in production, use a more secure method)
        private string HashPassword(string password)
        {
            // For simplicity - in production use a proper hashing library
            /*return Convert.ToBase64String(
                System.Security.Cryptography.SHA256.Create()
                .ComputeHash(System.Text.Encoding.UTF8.GetBytes(password))
            );
            */
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private bool VerifyPassword(string enteredPassword, string storedHash)
        {
            //var hashedEnteredPassword = HashPassword(enteredPassword);
            //return hashedEnteredPassword == storedHash;
            return BCrypt.Net.BCrypt.Verify(enteredPassword, storedHash);

        }

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
    }
}