﻿using Microsoft.AspNetCore.Mvc;
using EReaderApp.Data;
using EReaderApp.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;

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

            // Create claims for the user
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.Name),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.NameIdentifier, user.IdUser.ToString()),
                new Claim(ClaimTypes.Role, user.Role) // Add role claim
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // Remember me functionality
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

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

                // Hash the password
                model.Password = HashPassword(model.Password);

                // Set the creation date
                model.CreatedAt = DateTime.Now;

                // Set default role to "User"
                model.Role = "User";

                _context.Users.Add(model);
                await _context.SaveChangesAsync();

                // Auto-login after registration
                await LoginUserAfterRegistration(model);

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

        // Simple password hashing (in production, use a more secure method)
        private string HashPassword(string password)
        {
            // For simplicity - in production use a proper hashing library
            return Convert.ToBase64String(
                System.Security.Cryptography.SHA256.Create()
                .ComputeHash(System.Text.Encoding.UTF8.GetBytes(password))
            );
        }

        private bool VerifyPassword(string enteredPassword, string storedHash)
        {
            var hashedEnteredPassword = HashPassword(enteredPassword);
            return hashedEnteredPassword == storedHash;
        }

        private async Task LoginUserAfterRegistration(User user)
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
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);
        }
    }
}