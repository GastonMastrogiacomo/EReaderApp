using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class Review
    {
        [Key]
        public int IdReview { get; set; }

        public string? Comment { get; set; }

        [Required]
        public int FKIdBook { get; set; }

        [Required]
        public int FKIdUser { get; set; }

        [ForeignKey("FKIdBook")]
        public Book Book { get; set; }

        [ForeignKey("FKIdUser")]
        public User User { get; set; }
    }
}
