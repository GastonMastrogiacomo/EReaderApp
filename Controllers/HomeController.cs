using EReaderApp.Data;
using EReaderApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;

namespace EReaderApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            var recommendedBooks = _context.Books
                                           .OrderBy(b => b.IdBook)
                                           .Take(5)
                                           .ToList();

            var popularBooks = _context.Books
                                       .OrderByDescending(b => b.Score)
                                       .Take(6)
                                       .ToList();

            ViewBag.RecommendedBooks = recommendedBooks;
            ViewBag.PopularBooks = popularBooks;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
