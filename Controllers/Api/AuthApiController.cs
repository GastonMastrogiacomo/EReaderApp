using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EReaderApp.Data;
using EReaderApp.Models;
using EReaderApp.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace EReaderApp.Controllers.Api
{
    [Route("api/auth")]
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IJwtService _jwtService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthApiController> _logger;

        public AuthApiController(
            ApplicationDbContext context,
            IJwtService jwtService,
            IConfiguration configuration,
            ILogger<AuthApiController> logger)
        {
            _context = context;
            _jwtService = jwtService;
            _configuration = configuration;
            _logger = logger;
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

        [HttpPost("google")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid request data", errors = ModelState });
            }

            try
            {
                // Verify Google ID token using HTTP endpoint
                var payload = await VerifyGoogleTokenAsync(request.IdToken);

                if (payload == null || string.IsNullOrEmpty(payload.Email))
                {
                    return Unauthorized(new { success = false, message = "Invalid Google token" });
                }

                // Find or create user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == payload.Email);

                if (user == null)
                {
                    // Create new user from Google data
                    user = new User
                    {
                        Name = payload.Name ?? "Google User",
                        Email = payload.Email,
                        Password = HashPassword(Guid.NewGuid().ToString()), // Random password since they use Google auth
                        CreatedAt = DateTime.Now,
                        Role = "User",
                        ProfilePicture = payload.Picture
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Created new user from Google authentication: {Email}", payload.Email);
                }
                else
                {
                    _logger.LogInformation("Existing user logged in with Google: {Email}", payload.Email);
                }

                // Generate JWT token
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Google authentication failed for token: {Token}",
                    request.IdToken?.Length > 20 ? request.IdToken.Substring(0, 20) + "..." : request.IdToken);
                return StatusCode(500, new { success = false, message = "Authentication failed", error = ex.Message });
            }
        }

        private async Task<GoogleTokenPayload?> VerifyGoogleTokenAsync(string idToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                // Use Google's tokeninfo endpoint to verify the token
                var response = await httpClient.GetAsync($"https://oauth2.googleapis.com/tokeninfo?id_token={idToken}");

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Google token verification failed with status: {StatusCode}, Content: {Content}",
                        response.StatusCode, errorContent);
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };

                var tokenInfo = JsonSerializer.Deserialize<GoogleTokenPayload>(jsonResponse, options);

                if (tokenInfo == null)
                {
                    _logger.LogWarning("Failed to deserialize Google token response");
                    return null;
                }

                // Verify the audience (client ID)
                var googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")
                    ?? _configuration["Authentication:Google:ClientId"];

                if (string.IsNullOrEmpty(googleClientId))
                {
                    _logger.LogError("Google Client ID not configured");
                    return null;
                }

                if (tokenInfo.Aud != googleClientId && tokenInfo.Azp != googleClientId)
                {
                    _logger.LogWarning("Google token audience mismatch. Expected: {Expected}, Actual Aud: {Aud}, Actual Azp: {Azp}",
                        googleClientId, tokenInfo.Aud, tokenInfo.Azp);
                    return null;
                }

                // Check if token is expired
                if (!string.IsNullOrEmpty(tokenInfo.Exp))
                {
                    if (long.TryParse(tokenInfo.Exp, out long expTimestamp))
                    {
                        if (expTimestamp < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                        {
                            _logger.LogWarning("Google token has expired. Exp: {Exp}, Now: {Now}",
                                expTimestamp, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                            return null;
                        }
                    }
                }

                return tokenInfo;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error while verifying Google token");
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout while verifying Google token");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Google token response");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while verifying Google token");
                return null;
            }
        }

        [HttpPost("validate")]
        public IActionResult ValidateToken()
        {
            if (User.Identity?.IsAuthenticated == true)
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

    // Request models
    public class LoginRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(8)]
        public string Password { get; set; } = string.Empty;
    }

    public class GoogleLoginRequest
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;
    }

    // Google token payload model for HTTP-based verification
    public class GoogleTokenPayload
    {
        public string? Iss { get; set; }        // Issuer
        public string? Azp { get; set; }        // Authorized party
        public string? Aud { get; set; }        // Audience (Client ID)

        public string? Exp { get; set; }        // Expiration time (as string)
        public string? Iat { get; set; }        // Issued at (as string)

        public string? Sub { get; set; }        // Subject (User ID)
        public string? Email { get; set; }      // User email

        [System.Text.Json.Serialization.JsonPropertyName("email_verified")]
        public bool EmailVerified { get; set; } // Email verification status

        public string? Name { get; set; }       // Full name
        public string? Picture { get; set; }    // Profile picture URL

        [System.Text.Json.Serialization.JsonPropertyName("given_name")]
        public string? GivenName { get; set; }  // First name

        [System.Text.Json.Serialization.JsonPropertyName("family_name")]
        public string? FamilyName { get; set; } // Last name
    }
}