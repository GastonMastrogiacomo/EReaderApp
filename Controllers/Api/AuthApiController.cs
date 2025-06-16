using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EReaderApp.Data;
using EReaderApp.Models;
using EReaderApp.Services;
using System.ComponentModel.DataAnnotations;

namespace EReaderApp.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJwtService _jwtService;

        public AuthApiController(ApplicationDbContext context, IJwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request data", errors = ModelState });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user == null || !VerifyPassword(request.Password, user.Password))
            {
                return Unauthorized(new { success = false, message = "Invalid email or password" });
            }

            var token = _jwtService.GenerateToken(user);

            return Ok(new
            {
                success = true,
                token = token,
                user = new
                {
                    id = user.IdUser,
                    name = user.Name,
                    email = user.Email,
                    role = user.Role,
                    profilePicture = user.ProfilePicture
                },
                expiresIn = 7 * 24 * 60 * 60 // 7 days in seconds
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request data", errors = ModelState });
            }

            // Check if user already exists
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                return BadRequest(new { success = false, message = "Email is already in use" });
            }

            // Validate password strength
            if (!IsPasswordValid(request.Password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Password must be at least 8 characters long and include uppercase letters, lowercase letters, numbers, and special characters."
                });
            }

            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                Password = HashPassword(request.Password),
                CreatedAt = DateTime.Now,
                Role = "User"
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = _jwtService.GenerateToken(user);

            return Ok(new
            {
                success = true,
                token = token,
                user = new
                {
                    id = user.IdUser,
                    name = user.Name,
                    email = user.Email,
                    role = user.Role,
                    profilePicture = user.ProfilePicture
                },
                expiresIn = 7 * 24 * 60 * 60 // 7 days in seconds
            });
        }

        [HttpPost("validate")]
        public IActionResult ValidateToken()
        {
            // This endpoint can be used to validate if the current token is still valid
            // The JWT middleware will handle the validation
            if (User.Identity.IsAuthenticated)
            {
                return Ok(new
                {
                    success = true,
                    valid = true,
                    user = new
                    {
                        id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                        name = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value,
                        email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
                        role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
                    }
                });
            }

            return Unauthorized(new { success = false, valid = false, message = "Invalid or expired token" });
        }

        private bool VerifyPassword(string enteredPassword, string storedHash)
        {
            return BCrypt.Net.BCrypt.Verify(enteredPassword, storedHash);
        }

        private string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        private bool IsPasswordValid(string password)
        {
            if (password.Length < 8) return false;
            if (!password.Any(char.IsUpper)) return false;
            if (!password.Any(char.IsLower)) return false;
            if (!password.Any(char.IsDigit)) return false;
            if (!password.Any(c => !char.IsLetterOrDigit(c))) return false;
            return true;
        }
    }

    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }

    public class RegisterRequest
    {
        [Required]
        public string Name { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [MinLength(8)]
        public string Password { get; set; }
    }
}