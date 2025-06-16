using EReaderApp.Data;
using EReaderApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

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

        public async Task<IActionResult> Index()
        {
            // Get categories with book counts
            var categories = await _context.Categories
                .Select(c => new {
                    Category = c,
                    BookCount = _context.BookCategories.Count(bc => bc.FKIdCategory == c.IdCategory)
                })
                .Where(x => x.BookCount > 0) // Only show categories that have books
                .OrderByDescending(x => x.BookCount)
                .Take(5) // Show top 5 categories
                .Select(x => x.Category)
                .ToListAsync();

            // Get recently added books (last 2 weeks, ordered by ID)
            var recentlyAddedBooks = await _context.Books
                .OrderByDescending(b => b.IdBook)
                .Take(6)
                .ToListAsync();

            // Get most popular books based on average review scores
            var popularBooksQuery = from book in _context.Books
                                    let avgRating = _context.Reviews
                                        .Where(r => r.FKIdBook == book.IdBook)
                                        .Select(r => (double?)r.Rating)
                                        .Average()
                                    let reviewCount = _context.Reviews
                                        .Count(r => r.FKIdBook == book.IdBook)
                                    where reviewCount >= 1 // Must have at least 1 review
                                    orderby avgRating descending, reviewCount descending
                                    select new
                                    {
                                        Book = book,
                                        AverageRating = avgRating ?? 0,
                                        ReviewCount = reviewCount
                                    };

            var popularBooksWithRatings = await popularBooksQuery.Take(6).ToListAsync();
            var popularBooks = popularBooksWithRatings.Select(x => x.Book).ToList();

            // If we don't have enough books with reviews, supplement with highest scored books
            if (popularBooks.Count < 6)
            {
                var additionalBooks = await _context.Books
                    .Where(b => !popularBooks.Select(pb => pb.IdBook).Contains(b.IdBook))
                    .Where(b => b.Score.HasValue)
                    .OrderByDescending(b => b.Score)
                    .Take(6 - popularBooks.Count)
                    .ToListAsync();

                popularBooks.AddRange(additionalBooks);
            }

            // Pass data to view
            ViewBag.Categories = categories;
            ViewBag.RecentlyAddedBooks = recentlyAddedBooks;
            ViewBag.PopularBooks = popularBooks;
            ViewBag.PopularBooksWithRatings = popularBooksWithRatings;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult About()
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