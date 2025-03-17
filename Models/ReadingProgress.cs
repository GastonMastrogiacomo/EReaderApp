using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class ReadingProgress
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int BookId { get; set; }

        [Required]
        public int CurrentPage { get; set; }

        public int TotalPages { get; set; }

        [Required]
        public DateTime StartedAt { get; set; }

        [Required]
        public DateTime LastReadAt { get; set; }

        public bool IsCompleted { get; set; }

        public decimal CompletionPercentage { get; set; }

        // Tiempo total de lectura en minutos
        public int TotalReadingTimeMinutes { get; set; }

        // Relaciones
        [ForeignKey("UserId")]
        public User User { get; set; }

        [ForeignKey("BookId")]
        public Book Book { get; set; }
    }
}