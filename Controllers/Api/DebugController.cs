using Microsoft.AspNetCore.Mvc;

namespace EReaderApp.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        [HttpGet("test")]
        public IActionResult Test()
        {
            return Ok(new
            {
                success = true,
                message = "API routing is working!",
                timestamp = DateTime.UtcNow,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                server = "Render.com"
            });
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            return Ok(new
            {
                success = true,
                message = "Debug API Controller is working!",
                availableEndpoints = new[]
                {
                    "GET /api/debug/test",
                    "GET /api/debug",
                    "GET /api/books",
                    "POST /api/auth/login",
                    "POST /api/auth/google"
                }
            });
        }
    }
}