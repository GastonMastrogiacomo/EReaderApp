using System.ComponentModel.DataAnnotations;

namespace EReaderApp.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalUsers { get; set; }
        public int TotalBooks { get; set; }
        public int TotalCategories { get; set; }
        public int TotalPublications { get; set; }
        public List<User> RecentUsers { get; set; }
        public List<Book> RecentBooks { get; set; }
    }
}
