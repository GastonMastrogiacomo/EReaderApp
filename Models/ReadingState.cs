using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class ReadingState
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int BookId { get; set; }

        [Required]
        public int CurrentPage { get; set; }

        public float ZoomLevel { get; set; } = 1.0f;

        public string ViewMode { get; set; } = "doublePage";

        public DateTime LastAccessed { get; set; } = DateTime.Now;

        // Relaciones
        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [ForeignKey("BookId")]
        public virtual Book Book { get; set; }
    }
}