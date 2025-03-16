using System.ComponentModel.DataAnnotations;

namespace EReaderApp.Models
{
    public class Category
    {
        [Key]
        public int IdCategory { get; set; }

        [Required]
        public string CategoryName { get; set; }
    }
}
