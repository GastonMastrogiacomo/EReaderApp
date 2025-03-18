using Microsoft.AspNetCore.Mvc;
using EReaderApp.Data;
using EReaderApp.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace EReaderApp.Controllers
{
    [Authorize]
    public class ReaderSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReaderSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Obtener configuración del lector
        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var settings = await _context.ReaderSettings
                .FirstOrDefaultAsync(rs => rs.UserId == userId);

            if (settings == null)
            {
                // Crear configuración predeterminada
                settings = new ReaderSettings
                {
                    UserId = userId,
                    FontSize = 16,
                    FontFamily = "Arial",
                    Theme = "light"
                };

                _context.ReaderSettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            return Json(settings);
        }

        // Actualizar configuración del lector
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSettings(string theme, string fontFamily, int fontSize)
        {
            if (string.IsNullOrEmpty(theme) || string.IsNullOrEmpty(fontFamily) || fontSize <= 0)
                return BadRequest("Valores de configuración no válidos");

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var settings = await _context.ReaderSettings
                .FirstOrDefaultAsync(rs => rs.UserId == userId);

            if (settings == null)
            {
                // Crear nueva configuración
                settings = new ReaderSettings
                {
                    UserId = userId,
                    FontSize = fontSize,
                    FontFamily = fontFamily,
                    Theme = theme
                };

                _context.ReaderSettings.Add(settings);
            }
            else
            {
                // Actualizar configuración existente
                settings.FontSize = fontSize;
                settings.FontFamily = fontFamily;
                settings.Theme = theme;

                _context.ReaderSettings.Update(settings);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, settings });
        }
    }
}