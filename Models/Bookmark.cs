using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class Bookmark
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int BookId { get; set; }

        [Required]
        public int PageNumber { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Relaciones
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [ForeignKey("BookId")]
        public virtual Book Book { get; set; }
    }
}