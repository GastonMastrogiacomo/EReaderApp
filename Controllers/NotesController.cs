using Microsoft.AspNetCore.Mvc;
using EReaderApp.Data;
using EReaderApp.Models;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace EReaderApp.Controllers
{
    [Authorize]
    public class NotesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // API para obtener notas de un libro
        [HttpGet]
        public async Task<IActionResult> GetBookNotes(int bookId)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var notes = await _context.Notes
                .Where(n => n.BookId == bookId && n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return Json(notes);
        }

        // API para crear una nueva nota - permite recibir datos de FormData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateNote(int bookId, string content)
        {
            if (string.IsNullOrEmpty(content))
                return BadRequest("El contenido de la nota no puede estar vacío");

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var note = new Note
            {
                BookId = bookId,
                UserId = userId,
                Content = content,
                CreatedAt = DateTime.Now
            };

            _context.Notes.Add(note);
            await _context.SaveChangesAsync();

            return Json(new { success = true, note });
        }

        // API para actualizar una nota - permite recibir datos de FormData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateNote(int id, string content)
        {
            if (string.IsNullOrEmpty(content))
                return BadRequest("El contenido de la nota no puede estar vacío");

            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (note == null)
                return NotFound("Nota no encontrada");

            note.Content = content;
            note.UpdatedAt = DateTime.Now;

            _context.Notes.Update(note);
            await _context.SaveChangesAsync();

            return Json(new { success = true, note });
        }

        // API para eliminar una nota - permite recibir datos de FormData
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteNote(int id)
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (note == null)
                return NotFound("Nota no encontrada");

            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // Para ver todas las notas del usuario
        public async Task<IActionResult> Index()
        {
            int userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var notes = await _context.Notes
                .Where(n => n.UserId == userId)
                .Include(n => n.Book)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(notes);
        }
    }
}