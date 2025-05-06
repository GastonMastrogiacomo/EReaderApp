using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EReaderApp.Models
{
    public class ReadingActivity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int BookId { get; set; }

        public DateTime FirstAccess { get; set; }

        public DateTime LastAccess { get; set; }

        public int AccessCount { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }

        [ForeignKey("BookId")]
        public virtual Book Book { get; set; }
        public int TotalPagesRead { get; set; }
        public int LastPageRead { get; set; }
        public int TotalReadingTimeMinutes { get; set; }

        // Add calculated properties
        [NotMapped]
        public double AveragePagesPerSession => AccessCount > 0 ? (double)TotalPagesRead / AccessCount : 0;

        [NotMapped]
        public double AverageTimePerSessionMinutes => AccessCount > 0 ? (double)TotalReadingTimeMinutes / AccessCount : 0;

        [NotMapped]
        public double CompletionPercentage => (Book?.PageCount > 0 ? (double)LastPageRead / Book.PageCount * 100 : 0) ?? 0;
    }
}